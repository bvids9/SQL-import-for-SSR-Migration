using System;
using System.Collections.Generic;

public class MockArasRepository : IArasRepository
{
    private readonly ILogger _logger;

    public MockArasRepository(ILogger logger)
    {
        _logger = logger;
    }

    public void DeleteQueryDefinition(string name)
    {
        _logger.Log($"DELETE qry_QueryDefinition WHERE name = '{name}'");
    }

    public string CreateQueryDefinition(string name, string description)
    {
        var id = GenerateMockId();
        _logger.Log($"CREATE qry_QueryDefinition name='{name}' description='{description}' → id={id}");
        return id;
    }

    public string GetItemTypeId(string name)
    {
        var id = GenerateMockId();
        _logger.Log($"GET ItemType name='{name}' → id={id}");
        return id;
    }

    public string AddQueryItem(string qryDefId, string refId, string alias, string itemTypeId, string filterXml)
    {
        var id = GenerateMockId();
        _logger.Log($"ADD qry_QueryItem alias='{alias}' itemTypeId='{itemTypeId}' filterXml='{filterXml}' → id={id}");
        return id;
    }

    public void AddQueryItemProperty(string qryItemId, string propertyName)
    {
        _logger.Log($"ADD qry_QueryItemSelectProperty propertyName='{propertyName}' to qryItemId='{qryItemId}'");
    }

    public void AddQueryReference(string qryDefId, string childRefId, string parentRefId, string filterXml, string refId)
    {
        _logger.Log($"ADD qry_QueryReference childRefId='{childRefId}' parentRefId='{parentRefId}' filterXml='{filterXml}'");
    }

    public void DeleteReport(string name)
    {
        _logger.Log($"DELETE rpt_Report WHERE name = '{name}'");
    }

    public string CreateReport(string name, string description, string queryId, string contextId)
    {
        var id = GenerateMockId();
        _logger.Log($"CREATE rpt_Report name='{name}' queryId='{queryId}' contextId='{contextId}' → id={id}");
        return id;
    }

    public void AddReportIdentityShare(string reportId, string identityId, string shareMode)
    {
        _logger.Log($"ADD rpt_Report IdentityShare identityId='{identityId}' shareMode='{shareMode}'");
    }

    public List<(string IdentityId, string AccessRights)> GetSsrSharedIdentities(string ssrName)
    {
        _logger.Log($"GET SelfServiceReportSharedWith for SSR='{ssrName}' → returning empty list");
        return new List<(string, string)>();
    }

    public List<(string IdentityId, string ShareMode)> GetReportSharedIdentities(string reportName)
    {
        _logger.Log($"GET rpt_Report IdentityShare for report='{reportName}' → returning empty list");
        return new List<(string, string)>();
    }

    private string GenerateMockId() =>
        Guid.NewGuid().ToString("N").ToUpper();
}