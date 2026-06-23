using System;
using System.Collections.generic;
using Aras.IOM;

public class ArasRepository : IArasRepository
{
    private readonly Innovator _inn;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises the repository with an Aras Innovator connection.
    /// </summary>
    /// <param name="inn">The Aras Innovator instance from this.getInnovator()</param>
    /// <param name="logger">Use ArasLogger (CCO) in production</param>
    public ArasRepository(Innovator inn, ILogger logger)
    {
        _inn = inn;
        _logger = logger;
    }

    /// <summary>
    /// Deletes an existing Query Definition by name if it exists.
    /// </summary>
    /// <param name="name">The name of the Query Definition to delete</param>
    public void DeleteQueryDefinition(string name)
    {
        _logger.log($"Deleting Query Definition: {name}");
        Item item = _inn.newItem("qry_QueryDefinition", "delete");
        item.setAttribute("where", $"[qry_QueryDefinition].name = '{name}'");
        item.apply();
    }

    /// <summary>
    /// Creates a new Query Definition item in Aras.
    /// </summary>
    /// <param name="name">The name of the Query Definition</param>
    /// <param name="description">Optional description, maps to Title in SqlToReportOptions</param>
    /// <returns>The ID of the created Query Definition item, or null if failed</returns>
    public string CreateQueryDefinition(string name, string description)
    {
        _logger.Log($"Creating Query Definition: {name}");
        Item item = _inn.newItem("qry_QueryDefinition", "add");
        item.setProperty("name", name);
        if (!string.IsNullOrWhiteSpace(description))
        {
            item.setProperty("description", description);
        }
        item = item.apply();
        if (item.isError())
        {
            _logger.LogError($"Error creating Query Definition: {item.getErrorDetail()}");
            return null;
        }
        return item.getID();
    }

    /// <summary>
    /// Looks up an ItemType in Aras by name.
    /// </summary>
    /// <param name="name">The ItemType name to find</param>
    /// <returns>The ID of the ItemType, or null if not found</returns>
    public string GetItemTypeId(string name)
    {
        _logger.Log($"Looking up ItemType: {name}");
        GetItemTypeId item = _inn.newItem("ItemType", "get");
        item.setProperty("name", name);
        item = item.apply();
        if (item.isError())
        {
            _logger.LogError($"ItemType not found: {name}");
            return null;
        }
        return item.getID();
    }

    /// <summary>
    /// Adds a Query Item to a Query Definition.
    /// </summary>
    /// <param name="qryDefId">The ID of the parent Query Definition</param>
    /// <param name="refId">The ref_id of the Query Item</param>
    /// <param name="alias">The alias for the Query Item</param>
    /// <param name="itemTypeId">The ID of the ItemType</param>
    /// <param name="filterXml">Optional WHERE filter XML</param>
    /// <returns>The ID of the created Query Item</returns>
    public string AddQueryItem(string qryDefId, string refId, string alias, string itemTypeId, string filterXml)
    {
        _logger.Log($"Adding Query Item: alais={alias} itemTypeId={itemTypeId}");
        Item qryDefItem = _inn.newItem("qry_QueryDefinition", "get");
        qryDefItem.setID(qryDefId);
        qryDefItem.apply();
        if (qryDefItem.isError()) {
            _logger.LogError($"Error fetching Query Definition: {qryDefItem.getErrorDetail()}");
            return null;
        }

        Item qryItem = qryDefItem.createRelationship("qry_QueryItem", "add");
        qryItem.setProperty("ref_id", refId);
        qryItem.setProperty("alias", alias);
        qryItem.setProperty("item_type", itemTypeId);

        if (!string.IsNullOrWhiteSpace(filterXml))
        {
            qryItem.setProperty("filter_xml", filterXml);
        }

        qryItem = qryItem.apply();
        if (qryItem.isError())
        {
            _logger.LogError($"Error adding Query Item: {qryItem.getErrorDetail()}");
            return null;
        }

        return qryItem.getID();
    }

    /// <summary>
    /// Adds a selected property to a Query Item.
    /// </summary>
    /// <param name="qryItemId">The ID of the parent Query Item</param>
    /// <param name="propertyName">The property name to select</param>
    public void AddQueryItemProperty(string qryItemId, string propertyName)
    {
        _logger.Log($"Adding Query Item Property: {propertyName}");
        Item qryItem = _inn.newItem("qry_QueryItem", "get");
        qryItem.setID(qryItemId);
        qryItem = qryItem.apply();
        
        if(qryItem.isError())
        {
            _logger.LogError($"Error fetching Query Item: {qryItem.getErrorDetail()}");
            return;
        }

        Item prop = qryItem.createRelationship("qry_QueryItemSelectProperty", "add");
        prop.setProperty("property_name", propertyName);
        prop.apply();
    }

    /// <summary>
    /// Adds a Query Reference defining the JOIN relationship between two Query Items.
    /// </summary>
    /// <param name="qryDefId">The ID of the parent Query Definition</param>
    /// <param name="childRefId">The ref_id of the child Query Item</param>
    /// <param name="parentRefId">The ref_id of the parent Query Item, null for root table</param>
    /// <param name="filterXml">The JOIN filter XML</param>
    /// <param name="refId">The ref_id of the Query Reference</param>
    public void AddQueryReference(string qryDefId, string childRefId, string parentRefId, string filterXml, string refId)
    {
        _logger.Log($"Adding Query Reference: childRefId={childRefId} parentRefId={parentRefId} filterXml={filterXml} refId={refId}");
        Item qryDefItem = _inn.newItem("qry_QueryDefinition", "get");
        qryDefItem.setID(qryDefId);
        qryDefItem = qryDefItem.apply();
        if (qryDefItem.isError())
        {
            _logger.LogError($"Error fetching Query Definition: {qryDefItem.getErrorDetail()}");
            return;
        }

        Item qryReference = qryDefItem.createRelationship("qry_QueryReference", "add");
        qryReference.setProperty("child_ref_id", childRefId);

        if (!string.IsNullOrWhiteSpace(parentRefId))
        {
            qryReference.setProperty("parent_ref_id", parentRefId);
            qryReference.setProperty("filter_xml", filterXml);
            qryReference.setProperty("ref_id", refId);
        }
        qryReference.apply();

    }

    /// <summary>
    /// Deletes an existing Report by name if it exists.
    /// </summary>
    /// <param name="name">The name of the Report to delete</param>
    public void DeleteReport(string name)
    {
        _logger.Log($"Deleting Report: {name}");
        Item item = _inn.newItem("rpt_Report", "delete");
        item.setAttribute("where", $"[rp_Report]._name = '{name}'");
        item.apply();
    }

    /// <summary>
    /// Creates a new Report in Aras linked to a Query Definition.
    /// </summary>
    /// <param name="name">The name of the Report</param>
    /// <param name="description">Optional description</param>
    /// <param name="queryId">The ID of the linked Query Definition</param>
    /// <param name="contextId">The ID of the context ItemType</param>
    /// <returns>The ID of the created Report, or null if failed</returns>
    public string CreateReport(string name, string description, string queryId, string contextId)
    {
        _logger.Log($"Creating Report {name}");
        Item item = _inn.newItem("rpt_Report", "add");
        item.setProperty("_name", name);
        
        if (!string.IsNullOrWhiteSpace(description))
        {
            item.setProperty("_description", description);
        }

        item.setProperty("generate_report_metadata", "1");
        item.setProperty("_query", queryId);
        item.setProperty("_context", contextId);
        item = item.apply();

        if (item.isError())
        {
            _logger.LogError($"Error creating Report: {item.getErrorDetail()}");
            return null;
        }

        return item.getID();
    }


    /// <summary>
    /// Adds an identity share to a Report.
    /// </summary>
    /// <param name="reportId">The ID of the Report</param>
    /// <param name="identityId">The ID of the Identity to share with</param>
    /// <param name="shareMode">Viewer or Editor</param>
    public void AddReportIdentityShare(string reportId, string identityId, string shareMode)
    {
        _logger.Log($"Adding Report Identity Share: identityId={identityId} shareMode={shareMode}");
        Item report = _inn.newItem("rpt_Report", "get");
        report.setID(reportId);
        report = report.apply();
        if (report.isError())
        {
            _logger.LogError($"Error fetching Report: {report.getErrorDetail()}");
            return;
        }

        Item permShare = report.createRelationship("rpt_Report IdentityShare", "add");
        permShare.setProperty("share_mode", shareMode);
        permShare.setProperty("related_id", identityId);
        permShare.apply();
    }

    /// <summary>
    /// Gets the shared identities from an existing SSR report to inherit permissions.
    /// </summary>
    /// <param name="ssrName">The name of the SSR report</param>
    /// <returns>List of (IdentityId, AccessRights) tuples</returns>
    public List<(string IdentityId, string AccessRights)> GetSsrSharedIdentities(string ssrName)
    {
        _logger.Log($"Getting SSR shared identities for: {ssrName}");
        List<(string, string)> result = new List<(string, string)>();

        Item ssrReport = _inn.newItem("SelfServiceReport", "get");
        ssrReport.setProperty("name", ssrName);
        ssrReport = ssrReport.apply();
        if (ssrReport.isError())
        {
            _logger.LogError($"SSR Report not found: {ssrName}");
            return result;
        }

        ssrReport.fetchRelationships("SelfServiceReportSharedWith");
        Item shared = ssrReport.getRelationships("SelfServiceReportSharedWith");
        for (int i = 0; i < shared.getItemCount(); i++)
        {
            Item permission = shared.getItemByIndex(i);
            result.Add((permission.getProperty("related_id"), permission.getProperty("access_rights")));
        }

        return result;
    }

    /// <summary>
    /// Gets the shared identities from an existing rpt_Report to inherit permissions.
    /// </summary>
    /// <param name="reportName">The name of the rpt_Report</param>
    /// <returns>List of (IdentityId, ShareMode) tuples</returns>
    public List<(string IdentityId, string ShareMode)> GetReportSharedIdentities(string reportName)
    {
        _logger.Log($"Getting Report shared identities for: {reportName}");
        List<(string, string)> result = new List<(string, string)>();

        Item report = _inn.newItem("rpt_Report", "get");
        report.setProperty("_name", reportName);
        report = report.apply();
        if (report.isError())
        {
            _logger.LogError($"Report not found: {reportName}");
            return result;
        }

        report.fetchRelationships("rpt_Report IdentityShare");
        Item shared = report.getRelationships("rpt_Report IdentityShare");
        for (int i = 0; i < shared.getItemCount(); i++)
        {
            Item permission = shared.getItemByIndex(i);
            result.Add((permission.getProperty("related_id"), permission.getProperty("share_mode")));
        }

        return result;
    }
}