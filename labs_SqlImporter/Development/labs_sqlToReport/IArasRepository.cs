/// <summary>
/// Abstract implementation of the Aras Innovator Object Manager Class
/// Used for off-site testing outside of Innovator and allows compatibility with Aras
/// </summary>
public interface IArasRepository
{
    /// <summary>
    /// Deletes an existing Query Definition by name if it exists.
    /// </summary>
    /// <param name="name">The name of the Query Definition to delete</param>
    void DeleteQueryDefinition(string name);

    /// <summary>
    /// Creates a new Query Definition item in Aras.
    /// </summary>
    /// <param name="name">The name of the Query Definition</param>
    /// <param name="description">Optional description, maps to Title in SqlToReportOptions</param>
    /// <returns>The ID of the created Query Definition item</returns>
    string CreateQueryDefinition(string name, string description);

    /// <summary>
    /// Looks up an ItemType in Aras by name.
    /// </summary>
    /// <param name="name">The ItemType name to find</param>
    /// <returns>The ID of the ItemType, or null if not found</returns>
    string GetItemTypeId(string name);

    /// <summary>
    /// Adds a Query Item to a Query Definition.
    /// </summary>
    /// <param name="qryDefId">The ID of the parent Query Definition</param>
    /// <param name="refId">The ref_id of the Query Item</param>
    /// <param name="alias">The alias for the Query Item</param>
    /// <param name="itemTypeId">The ID of the ItemType</param>
    /// <param name="filterXml">Optional WHERE filter XML</param>
    /// <returns>The ID of the created Query Item</returns>
    string AddQueryItem(string qryDefId, string refId, string alias, string itemTypeId, string filterXml);

    /// <summary>
    /// Adds a selected property to a Query Item.
    /// </summary>
    /// <param name="qryItemId">The ID of the parent Query Item</param>
    /// <param name="propertyName">The property name to select</param>
    void AddQueryItemProperty(string qryItemId, string propertyName);

    /// <summary>
    /// Adds a Query Reference defining the JOIN relationship between two Query Items.
    /// </summary>
    /// <param name="qryDefId">The ID of the parent Query Definition</param>
    /// <param name="childRefId">The ref_id of the child Query Item</param>
    /// <param name="parentRefId">The ref_id of the parent Query Item, null for root table</param>
    /// <param name="filterXml">The JOIN filter XML</param>
    /// <param name="refId">The ref_id of the Query Reference</param>
    void AddQueryReference(string qryDefId, string childRefId, string parentRefId, string filterXml, string refId);

    /// <summary>
    /// Deletes an existing Report by name if it exists.
    /// </summary>
    /// <param name="name">The name of the Report to delete</param>
    void DeleteReport(string name);

    /// <summary>
    /// Creates a new Report in Aras linked to a Query Definition.
    /// </summary>
    /// <param name="name">The name of the Report</param>
    /// <param name="description">Optional description</param>
    /// <param name="queryId">The ID of the linked Query Definition</param>
    /// <param name="contextId">The ID of the context ItemType</param>
    /// <returns>The ID of the created Report</returns>
    string CreateReport(string name, string description, string queryId, string contextId);

    /// <summary>
    /// Adds an identity share to a Report.
    /// </summary>
    /// <param name="reportId">The ID of the Report</param>
    /// <param name="identityId">The ID of the Identity to share with</param>
    /// <param name="shareMode">Viewer or Editor</param>
    void AddReportIdentityShare(string reportId, string identityId, string shareMode);

    /// <summary>
    /// Gets the shared identities from an existing SSR report to inherit permissions.
    /// </summary>
    /// <param name="ssrName">The name of the SSR report</param>
    /// <returns>List of (identityId, accessRights) tuples</returns>
    List<(string IdentityId, string AccessRights)> GetSsrSharedIdentities(string ssrName);

    /// <summary>
    /// Gets the shared identities from an existing rpt_Report to inherit permissions.
    /// </summary>
    /// <param name="reportName">The name of the rpt_Report</param>
    /// <returns>List of (identityId, shareMode) tuples</returns>
    List<(string IdentityId, string ShareMode)> GetReportSharedIdentities(string reportName);

}