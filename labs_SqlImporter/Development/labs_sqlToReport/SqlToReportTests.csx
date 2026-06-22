#r "nuget: Microsoft.SqlServer.TransactSql.ScriptDom, 161.*"
#load "..\ILogger.cs"
#load "..\Models.cs"
#load "..\SqlToReportConverter.cs"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

// TESTING
var logger = new ConsoleLogger();
var converter = new SqlToReportConverter(logger);
var options = new SqlToReportOptions { Name = "Test", Title = "Test Report" };

var sql = @"SELECT
    p.item_number AS item_number,
    p.name AS name,
    p.state AS state,
    p.created_on AS created_on,
    u.keyed_name AS created_by,
    eco.item_number AS eco_number,
    eco.name AS eco_name,
    eco.state AS eco_state
FROM
    innovator.part p
    LEFT JOIN user u ON u.id = p.created_by_id
    LEFT JOIN eco eco ON eco.id = p.current_state
WHERE
    p.state = 'Released'";

var qryDef = converter.Generate(sql, options);

if (qryDef == null)
{
    Console.WriteLine("FAILED - check logs");
} else
{
    Console.WriteLine("SUCCESS.");
    converter.WriteQueryDefinition(qryDef, options);
    converter.WriteReportDefinition(qryDef, options);
}
