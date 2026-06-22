using System;
using System.Collections.Generic;

public class SqlToReportOptions
{
    public string InnovatorVersion;
    public string InnovatorDatabase;
    public string Name;
    public string Title;
    public bool PrettyAlias = true;
    public bool GenerateReport = true;
    public string SSRReport;
    public string RptReport;
}

public class QueryDefinition
{
    public List<TableDefinition> Tables = new List<TableDefinition>();

    public List<WhereDefinition> WhereDefinitionList = new List<WhereDefinition>();

    public string ContextItemID { get; set; }
}

public class TableDefinition
{
    public string ID { get; set; }

    public string RefID { get; set; }
    public TableDefinition ParentTable { get; set; }
    public string Name { get; set; }
    public string Alias { get; set; }

    public string PrettyAlias { get; set; }
    public string QualifiedName { get; set; }
    public string QualifiedNameOrAlias
    {
        get
        {
            return Alias?.ToLower() ?? QualifiedName?.ToLower();
        }
    }

    public string TableAliasForInnovator
    {
        get
        {
            string tableName = Alias?.ToLower() ?? Name.ToLower();

            return tableName.Length > 32 ? tableName.Substring(0, 32) : tableName;
        }

    }
    public string JoinType { get; set; }

    public string JoinFilter { get; set; }

    public bool WrapAndOnWhereFilter { get; set; } = false;

    public string WhereFilter { get; set; }

    public List<PropertyDefinition> Properties = new List<PropertyDefinition>();
}

public class PropertyDefinition
{
    public string Name { get; set; }
    public string Alias { get; set; }

    public string QualifiedName { get; set; }

}

public class WhereDefinition
{
    public string PropertyName { get; set; }
    public string Value { get; set; }

    public string ComparisonType { get; set; }
    public string ComparisonTypeConverter
    {
        get
        {
            switch (ComparisonType)
            {
                case "Equals":
                case "=":
                    return "eq";
                case "!=":
                    return "ne";
                case ">":
                    return "gt";
                case "<":
                    return "lt";
                case ">=":
                    return "ge";
                case "<=":
                    return "le";
                case "LIKE":
                    return "like";
                default:
                    return "eq";

            }
        }

    }
}