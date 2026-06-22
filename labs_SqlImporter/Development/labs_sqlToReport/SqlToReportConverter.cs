using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

/// <summary>
/// Converts Self Service Reporting SELECT statements into Aras Innovator Query Definition Objects
/// </summary>
public class SqlToReportConverter
{
    private readonly ILogger _logger;

    public SqlToReportConverter(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate a Query Definition Object from an SQL Query
    /// </summary>
    /// <param name="sql">Exported SQL statement of an Aras Self Service Report (SSR)</param>
    /// <param name="options">Name and configuration options for the Query Definition</param>
    /// <returns>QueryDefinition if parsing suceeded, null if parsing failed</returns>
    public QueryDefinition Generate(string sql, SqlToReportOptions options)
    {
        QueryDefinition qryDef = new QueryDefinition();

        // pre-processing to remove Aras schema prefix and escape any reserved words in SQL
        _logger.Log("Pre-processing SQL: stripping schema prefix.");
        sql = sql.Replace("innovator.", "").Replace(" user ", " [user] ");

        TSql150Parser parser = new TSql150Parser(false);
        IList<ParseError> parseErrors;
        TSqlFragment select = parser.Parse(new System.IO.StringReader(sql), out parseErrors);

        if (parseErrors.Any())
        {
            string errors = string.Join("\r\n", parseErrors.Select(e => e.Message));
            _logger.LogError(errors);
            return null;
        }

        var sqlScript = (TSqlScript)select;
        // check that only valid SELECT SQL statements are used.
        foreach (TSqlStatement sqlStatement in sqlScript.Batches.SelectMany(sqlBatch => sqlBatch.Statements))
        {
            if (sqlStatement.GetType() == typeof(SelectStatement))
            {
                if (ProcessSqlSelectStatement(sqlStatement, qryDef) != "success")
                {
                    _logger.LogError("Failed to process SELECT statement");
                    return null;
                }
            }
            else
            {
                _logger.LogError($"Query type not supported: {sqlStatement.GetType()}");
                return null;
            }
        }

        return qryDef;
    }

    /// <summary>
    /// Implementation of WriteQueryDefinition for live usage in Aras or in .NET test suite
    /// </summary>
    /// <param name="arasRepository"> Abstracted Aras Innovator Object Manager (i.e. Innovator inn object)</param>
    /// <param name="qryDef">The parsed Query Definition</param>
    /// <param name="options">Report name and configuration options</param>
    /// <returns></returns>
    public string WriteQueryDefinition(IArasRepository arasRepository, QueryDefinition qryDef, SqlToReportOptions options)
    {
        arasRepository.DeleteQueryDefinition(options.Name);
        string qryDefId = arasRepository.CreateQueryDefinition(options.Name, options.Title);

        List<string> fixedAliases = new List<string>();
        foreach (TableDefinition tableDef in qryDef.Tables) 
        {
            // Aras ItemType lookup
            string itemToFind = tableDef.Name.Replace("_", " ").ToLower().Trim();
            string itemTypeId = arasRepository.GetItemTypeId(itemToFind);
            if (itemTypeId == null)
            {
                itemTypeId = arasRepository.GetItemTypeId(itemToFind.Replace(" ", ""));
                if (itemTypeId == null)
                {
                    _logger.LogError($"ItemType {itemToFind} not found");
                    return null;
                }
            }

            // resolve Alias
            string alias;
            if (options.PrettyAlias)
            {
                if (!fixedAliases.Any(a => a.Equals(tableDef.Name)))
                {
                    int counter = qryDef.Tables.Count(t => t.Name == tableDef.Name);
                    tableDef.PrettyAlias = tableDef.Name;
                    if (counter > 1)
                    {
                        int count = 1;
                        foreach (TableDefinition tableRen in qryDef.Tables.Where(t => t.Name == tableDef.Name && t.ID != tableDef.ID))
                        {
                            tableRen.PrettyAlias = $"{tableDef.Name} {count}";
                            count++;
                        }
                        fixedAliases.Add(tableDef.Name);
                    }
                }
                alias = tableDef.PrettyAlias;
            } 
            else
            {
                alias = tableDef.TableAliasForInnovator;
            }

            // build WHERE filter for XML
            string filterXml = null;
            if (!string.IsNullOrWhiteSpace(tableDef.WhereFilter))
            {
                string filter = tableDef.WrapAndOnWhereFilter ? $"<and>{tableDef.WhereFilter}</and>" : tableDef.WhereFilter;
                filterXml = $"<condition>{filter}</condition>";
            }

            // add Query Item
            string qryItemId = arasRepository.AddQueryItem(qryDefId, tableDef.ID, alias, itemTypeId, filterXml);
            foreach (PropertyDefinition propDef in tableDef.Properties)
            {
                arasRepository.AddQueryItemProperty(qryItemId, propDef.Name);
            }

            // add Query Reference
            if (tableDef.ParentTable != null)
            {
                arasRepository.AddQueryReference(
                    qryDefId,
                    tableDef.ID,
                    tableDef.ParentTable.ID,
                    tableDef.JoinFilter,
                    tableDef.RefID);
            } 
            else
            {
                qryDef.ContextItemID = itemTypeId;
                arasRepository.AddQueryReference(qryDefId, tableDef.ID, null, null, null);
            }
        }

        _logger.Log($"Query Definition '{options.Name}' created with ID: {qryDefId}");
        return qryDefId;
    }

    /// <summary>
    /// Implementation of WriteReportDefinition for live usage in Aras or in .NET test suite
    /// </summary>
    /// <param name="arasRepository"> Abstracted Aras Innovator Object Manager (i.e. Innovator inn object)</param>
    /// <param name="qryDefId">The parsed Query Def Id</param>
    /// <param name="qryDef">The parsed Query Definition</param>
    /// <param name="options">Report name and configuration options</param>
    /// <returns></returns>
    public string WriteReportDefinition(IArasRepository arasRepository, string qryDefId, QueryDefinition qryDef, SqlToReportOptions options)
    {
        arasRepository.DeleteReport(options.Name);

        string reportId = arasRepository.CreateReport(options.Name, options.Title, qryDefId, qryDef.ContextItemID);
        if (reportId == null)
        {
            _logger.LogError($"Failed to create Report: {options.Name}");
            return null;
        }

        if (!string.IsNullOrEmpty(options.SSRReport))
        {
            List<(string identityId, string accessRights)> identities = arasRepository.GetSsrSharedIdentities(options.SSRReport);
            foreach ((string identityId, string accessRights) in identities)
            {
                string shareMode = (accessRights == "readonly" || accessRights == "viewonly") ? "Viewer" : "Editor";
                arasRepository.AddReportIdentityShare(reportId, identityId, shareMode);
            }
        }

        if (!string.IsNullOrEmpty(options.RptReport))
        {
            List<(string identityId, string shareMode)> identities = arasRepository.GetReportSharedIdentities(options.RptReport);
            foreach ((string identityId, string shareMode) in identities)
            {
                arasRepository.AddReportIdentityShare(reportId, identityId, shareMode);
            }
        }

        _logger.Log($"Report '{options.Name}' created with ID: {reportId}");
        return reportId;
    }

    private string ProcessSqlSelectStatement(TSqlStatement sqlStatement, QueryDefinition qryDef)
    {
        if (ExtractTablesAndAliases(((QuerySpecification)((SelectStatement)sqlStatement).QueryExpression).FromClause.TableReferences, qryDef) == "success")
        {
            if (ExtractTableProperties(sqlStatement, qryDef) == "success")
            {
                return ExtractWhereDefinition(((QuerySpecification)((SelectStatement)sqlStatement).QueryExpression).WhereClause, qryDef);
            }
        }
        return "Error processing SELECT statement"; // should include a generic error message
    }

    private string ExtractTablesAndAliases(IList<TableReference> tableReferences, QueryDefinition qryDef)
    {
        try
        {
            foreach (var tableReference in tableReferences)
            {
                if (tableReference is NamedTableReference namedTable)
                {
                    TableDefinition tableDef = new TableDefinition();

                    tableDef.Name = namedTable.SchemaObject.BaseIdentifier.Value.ToLower();
                    tableDef.Alias = namedTable.Alias?.Value.ToLower();
                    tableDef.QualifiedName = GetCombinedIdentifiers(namedTable.SchemaObject.Identifiers);
                    tableDef.ID = GetArasID();


                    qryDef.Tables.Add(tableDef);

                }
                else if (tableReference is QualifiedJoin qualifiedJoin)
                {

                    TableReference parentJoinTableRef = qualifiedJoin.SecondTableReference;
                    TableReference childJoinTableRef = qualifiedJoin.FirstTableReference;

                    ExtractTablesAndAliases(new List<TableReference> { childJoinTableRef }, qryDef);
                    ExtractTablesAndAliases(new List<TableReference> { parentJoinTableRef }, qryDef);

                    // Define Parent and Child and Analyze join

                    string parentJoinClause = null;
                    string childJoinClause = null;
                    List<WhereDefinition> whereDefinitions = null;

                    if (qualifiedJoin.SearchCondition is BooleanBinaryExpression binaryExpression)
                    {
                        string binaryExpressionType = binaryExpression.BinaryExpressionType.ToString().ToLower();
                        if (binaryExpressionType != "and")
                        {
                            _logger.LogError("OR Expression within join is not yet supported.");
                            return "OR Expression within join is not yet supported";
                        }


                        // Get Join
                        (string, string) joinClause = GetJoinClause((BooleanBinaryExpression)qualifiedJoin.SearchCondition);
                        parentJoinClause = joinClause.Item1;
                        childJoinClause = joinClause.Item2;

                        // Get Where
                        whereDefinitions = GetWhereClause(qualifiedJoin.SearchCondition, new List<WhereDefinition>());

                    }
                    else
                    {
                        (string, string) joinClause = GetJoinClause((BooleanComparisonExpression)qualifiedJoin.SearchCondition);
                        parentJoinClause = joinClause.Item1;
                        childJoinClause = joinClause.Item2;
                    }


                    TableDefinition parentTable = qryDef.Tables.FirstOrDefault(t => t.QualifiedNameOrAlias == GetStartIdentifier(parentJoinClause));
                    parentTable.JoinType = qualifiedJoin.QualifiedJoinType.ToString();

                    TableDefinition childTable = qryDef.Tables.FirstOrDefault(t => t.QualifiedNameOrAlias == GetStartIdentifier(childJoinClause));
                    if (parentTable != null)
                    {
                        childTable.RefID = GetArasID();
                        childTable.ParentTable = parentTable;
                        childTable.JoinType = qualifiedJoin.QualifiedJoinType.ToString();
                        childTable.JoinFilter = FormatXml($@"
                    <condition>
	                    <eq>
		                    <property query_items_xpath=""parent::Item"" name=""{GetEndIdentifier(parentJoinClause)}"" />
		                    <property name=""{GetEndIdentifier(childJoinClause)}"" />
	                    </eq>
                    </condition>");

                    }

                    // Manage Inner Join Type
                    if (childTable.JoinType == "Inner")
                    {
                        if (!string.IsNullOrWhiteSpace(parentTable.WhereFilter))
                        {
                            parentTable.WrapAndOnWhereFilter = true;
                            parentTable.WhereFilter += "\r\n";
                        }

                        parentTable.WhereFilter += FormatXml($@"
                    <exists>
			            <query_reference_path>{childTable.RefID}</query_reference_path>
		            </exists>");
                    }

                    // Manage Where on Join
                    SetWhereDefinition(whereDefinitions, parentTable);
                }
            }
            return "success";

        }
        catch (Exception ex)
        {
            _logger.LogError("Error extracting tables and aliases, please review your SQL Statement!", ex);
            return "Error extracting tables and aliases, please review your SQL Statement!";
        }
    }

    private void SetWhereDefinition(List<WhereDefinition> whereDefinitions, TableDefinition parentTable)
    {
        if (whereDefinitions != null)
        {
            if (!string.IsNullOrWhiteSpace(parentTable.WhereFilter) || whereDefinitions.Count() > 1)
            {
                parentTable.WrapAndOnWhereFilter = true;
                parentTable.WhereFilter += "\r\n";
            }

            foreach (WhereDefinition whereDefinition in whereDefinitions)
            {
                parentTable.WhereFilter += FormatXml($@"
                        <{whereDefinition.ComparisonTypeConverter}>
	                        <property name=""{GetEndIdentifier(whereDefinition.PropertyName)}"" />
                            <constant>{whereDefinition.Value}</constant>
                        </{whereDefinition.ComparisonTypeConverter}>");
            }
        }
    }

    private string ExtractTableProperties(TSqlStatement sqlStatement, QueryDefinition qryDef)
    {
        try
        {
            foreach (SelectElement selectElement in ((QuerySpecification)((SelectStatement)sqlStatement).QueryExpression).SelectElements)
            {
                PropertyDefinition propDef = new PropertyDefinition();
                propDef.Alias = ((SelectScalarExpression)selectElement).ColumnName.Value.ToLower();
                propDef.QualifiedName = GetCombinedIdentifiers(((ColumnReferenceExpression)((SelectScalarExpression)selectElement).Expression).MultiPartIdentifier.Identifiers);
                propDef.Name = GetEndIdentifier(propDef.QualifiedName);

                TableDefinition relatedTable = qryDef.Tables.FirstOrDefault(t => t.QualifiedNameOrAlias == GetStartIdentifier(propDef.QualifiedName)); // TODO: Potential null reference exception
                if (relatedTable != null)
                {
                    relatedTable.Properties.Add(propDef);
                }
                else
                {
                    _logger.LogError($"Table not found for property {propDef.QualifiedName}");
                    return $"Table not found for property {propDef.QualifiedName}";
                }

            }

            return "success";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error extracting table properties, please review your SQL Statement!", ex);
            return $"Error extracting table properties, please review your SQL Statement!";
        }

    }

    private string ExtractWhereDefinition(WhereClause whereClause, QueryDefinition qryDef)
    {
        if (whereClause != null)
        {
            qryDef.WhereDefinitionList = GetWhereClause(whereClause.SearchCondition, new List<WhereDefinition>());

            if (qryDef.WhereDefinitionList.Count > 0)
            {
                foreach (string propertyName in qryDef.WhereDefinitionList.Select(w => w.PropertyName).Distinct().ToList())
                {
                    TableDefinition parentTable = qryDef.Tables.FirstOrDefault(t => t.QualifiedNameOrAlias == GetStartIdentifier(propertyName));
                    SetWhereDefinition(qryDef.WhereDefinitionList.Where(w => w.PropertyName == propertyName).ToList(), parentTable);
                }
            }
        }

        return "success";
    }

    private (string, string) GetJoinClause(BooleanBinaryExpression expression)
    {

        if (expression.FirstExpression is BooleanComparisonExpression firstExpChild)
        {
            (string, string) joinClause = GetJoinClause(firstExpChild);
            if (joinClause.Item1 != null)
            {
                return joinClause;
            }
        }

        if (expression.FirstExpression is BooleanBinaryExpression firstBoolExpChild)
        {
            (string, string) joinClause = GetJoinClause(firstBoolExpChild);
            if (joinClause.Item1 != null)
            {
                return joinClause;
            }
        }

        if (expression.SecondExpression is BooleanComparisonExpression secondExpChild)
        {
            (string, string) joinClause = GetJoinClause(secondExpChild);
            if (joinClause.Item1 != null)
            {
                return joinClause;
            }
        }

        if (expression.SecondExpression is BooleanBinaryExpression secondBoolExpChild)
        {
            (string, string) joinClause = GetJoinClause(secondBoolExpChild);
            if (joinClause.Item1 != null)
            {
                return joinClause;
            }
        }

        _logger.LogError($"Join Expression too complex for this tool");
        return (null, null);
    }

    private (string, string) GetJoinClause(BooleanComparisonExpression boolExp)
    {
        if (boolExp.FirstExpression is ColumnReferenceExpression firstExpChild && boolExp.SecondExpression is ColumnReferenceExpression secondExpChild)
        {
            string childExp = GetCombinedIdentifiers(firstExpChild.MultiPartIdentifier.Identifiers);
            string parentExp = GetCombinedIdentifiers(secondExpChild.MultiPartIdentifier.Identifiers);
            return (parentExp, childExp);
        }

        return (null, null);
    }

    private List<WhereDefinition> GetWhereClause(TSqlFragment boolExp, List<WhereDefinition> whereDefList)
    {

        if (boolExp is BooleanComparisonExpression boolCompExp)
        {
            if (boolCompExp.FirstExpression is ColumnReferenceExpression columRef &&
               (boolCompExp.SecondExpression is StringLiteral || boolCompExp.SecondExpression is IntegerLiteral))
            {
                string propertyName = GetCombinedIdentifiers(columRef.MultiPartIdentifier.Identifiers);
                string value = null;
                if (boolCompExp.SecondExpression is StringLiteral str) { value = str.Value; }
                else if (boolCompExp.SecondExpression is IntegerLiteral i) { value = i.Value; }
                string comparisonType = boolCompExp.ComparisonType.ToString();

                whereDefList.Add(new WhereDefinition { PropertyName = propertyName, Value = value, ComparisonType = comparisonType });
            }

            GetWhereClause(((BooleanComparisonExpression)boolCompExp).FirstExpression, whereDefList);
            GetWhereClause(((BooleanComparisonExpression)boolCompExp).SecondExpression, whereDefList);

        }
        else if (boolExp is LikePredicate likePredicate)
        {
            if (likePredicate.FirstExpression is ColumnReferenceExpression columRef &&
               (likePredicate.SecondExpression is StringLiteral || likePredicate.SecondExpression is IntegerLiteral))
            {
                string propertyName = GetCombinedIdentifiers(columRef.MultiPartIdentifier.Identifiers);
                string value = null;
                if (likePredicate.SecondExpression is StringLiteral str) { value = str.Value; }
                else if (likePredicate.SecondExpression is IntegerLiteral i) { value = i.Value; }
                string comparisonType = "LIKE";

                whereDefList.Add(new WhereDefinition { PropertyName = propertyName, Value = value, ComparisonType = comparisonType });
            }

            GetWhereClause(((LikePredicate)likePredicate).FirstExpression, whereDefList);
            GetWhereClause(((LikePredicate)likePredicate).SecondExpression, whereDefList);
        }
        else if (boolExp is BooleanBinaryExpression boolBinaryExp)
        {
            GetWhereClause(((BooleanBinaryExpression)boolBinaryExp).FirstExpression, whereDefList);
            GetWhereClause(((BooleanBinaryExpression)boolBinaryExp).SecondExpression, whereDefList);
        }

        return whereDefList;
    }

    private string GetCombinedIdentifiers(IList<Identifier> identifiers)
    {
        return string.Join(".", identifiers.Select(item => item.Value)).ToLower();
    }

    private string GetEndIdentifier(string str)
    {
        if (str.Contains("."))
        {
            string[] parts = str.Split('.');
            return parts[parts.Length - 1];
        }
        else
        {
            return str;
        }

    }

    private string GetStartIdentifier(string str)
    {
        if (str.Contains("."))
        {
            string[] parts = str.Split('.');
            return string.Join(".", parts, 0, parts.Length - 1);
        }
        else
        {
            return str;
        }

    }

    private string GetArasID()
    {
        Guid guid = Guid.NewGuid();
        string guidWithoutHyphens = guid.ToString("N"); // "N" format removes hyphens

        return guidWithoutHyphens.ToUpper();
    }

    private string FormatXml(string xmlContent)
    {
        try
        {
            var xmlDoc = System.Xml.Linq.XDocument.Parse(xmlContent);
            return xmlDoc.ToString();
        }
        catch (XmlException)
        {
            _logger.LogError("Error parsing XML, returning original content.");
            return xmlContent; // Return the original content in case of an error
        }
    }
}