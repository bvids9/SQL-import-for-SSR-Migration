import xml.etree.ElementTree as ET
import os
import re

# Build paths for import
ROOT = os.path.join(os.path.dirname(__file__), '..')
DEV = os.path.join(ROOT, 'Development', 'labs_sqlToReport')
XML_TEMPLATE = os.path.join(ROOT, 'Import', 'Method', 'labs_sqlToReport.xml')
XML_OUTPUT = os.path.join(ROOT, 'Import', 'Method', 'labs_sqlToReport_built.xml')

# Dependencies
FILES = [
    os.path.join(DEV, 'ILogger.cs'),
    os.path.join(DEV, 'ArasLogger.cs'),
    os.path.join(DEV, 'IArasRepository.cs'),
    os.path.join(DEV, 'ArasRepository.cs'),
    os.path.join(DEV, 'Models.cs'),
    os.path.join(DEV, 'SqlToReportConverter.cs'),
]

# Aras entry point. Replaces the original entry point in the XML
ENTRY_POINT = """var inn = this.getInnovator();
var sql = this.getProperty("sql");
var qdName = this.getProperty("qdName");
var reportText = this.getProperty("reportBool");
var reportBool = false;
if (reportText == "true")
    reportBool = true;

var logger = new ArasLogger();
var repository = new ArasRepository(inn, logger);
var converter = new SqlToReportConverter(logger);

var options = new SqlToReportOptions();
options.Name = qdName;
options.Title = qdName;
options.PrettyAlias = true;
options.GenerateReport = reportBool;

var qryDef = converter.Generate(sql, options);
if (qryDef == null)
{
    return inn.newResult("Failed to parse SQL - check server log");
}

var qryDefId = converter.WriteQueryDefinition(repository, qryDef, options);
if (qryDefId == null)
{
    return inn.newResult("Failed to write Query Definition - check server log");
}

if (options.GenerateReport)
{
    var reportId = converter.WriteReportDefinition(repository, qryDefId, qryDef, options);
    if (reportId == null)
    {
        return inn.newResult("Failed to write Report - check server log");
    }
}

return inn.newResult("success");
}
"""

def strip_file(path):
    """
    Strip the usings, #r directives, #load directives and XML doc comments from a .cs file.
    Return parsed string
    """
    with open(path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    cleaned = []
    for line in lines:
        stripped = line.strip()
            
        # remove using statements
        if stripped.startswith ('using '):
            continue
            
        # remove #r directives
        if stripped.startswith('#r '):
            continue
            
        # remove #load directives
        if stripped.startswith('#load '):
            continue

        cleaned.append(line)

    return ''.join(cleaned)

def build_method():
    """
    Combines the entry point ald all .cs files into a single Aras Method block
    """
    print("Building Method...")
    parts = [ENTRY_POINT]
    
    for path in FILES:
        if not os.path.exists(path):
            print(f"WARNING: File not found: {path}")
            continue

        print(f"Including: {os.path.basename(path)}")

        code = strip_file(path)
        parts.append(f"\n// [+] {os.path.basename(path)}")
        parts.append(code)

    return '\n'.join(parts)

def build_xml(method_code):
    """ 
    Parse the XML template, replace the method_code CDATA block and output the XML
    """
    print(f"\nReading template: {XML_TEMPLATE}")

    with open(XML_TEMPLATE, 'r', encoding='utf-8') as f:
        content = f.read()

    # Replace content between CDATA tags
    pattern = r'(<method_code><!\[CDATA\[)(.*?)(\]\]></method_code>)'
    replacement = r'\g<1>' + method_code + r'\g<3>'
    updated = re.sub(pattern, replacement, content, flags=re.DOTALL)

    if updated == content:
        print ("WARNING: CDATA block not found in template - XML may not have been updated.")
    else:
        print("CDATA block replaced successfully.")
    
    print(f"\nWriting output: {XML_OUTPUT}")
    with open(XML_OUTPUT, 'w', encoding='utf-8') as f:
        f.write(updated)
    
    print("Done.")

if __name__ == '__main__':
    method_code = build_method()
    build_xml(method_code)
    print(f"\nOutput written to : {XML_OUTPUT}")
    print("Import labs_sqlToReport_built.xml using the Aras Package Tool.")