namespace SqlToReport.Tests
{
    public class SqlToReportConverterTests
    {
        private readonly SqlToReportConverter _converter;
        private readonly MockArasRepository _repository;

        public SqlToReportConverterTests()
        {
            _converter = new SqlToReportConverter(new ConsoleLogger());
            _repository = new MockArasRepository(new ConsoleLogger());
        }


        [Fact]
        public void SimpleSelect_ShouldReturnQueryDefinition()
        {
            string sql = @"SELECT p.item_number AS item_number
                        FROM part p";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });
            Assert.NotNull(result);
        }

        [Fact]
        public void SimpleSelect_ShouldReturnOneTable()
        {
            string sql = @"SELECT p.item_number AS item_number
                        FROM part p";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result);
            Assert.Single(result.Tables);
            Assert.Equal("part", result.Tables[0].Name);
            Assert.Equal("p", result.Tables[0].Alias);
            Assert.Single(result.Tables[0].Properties);
            Assert.Equal("item_number", result.Tables[0].Properties[0].Name);
        }

        [Fact]
        public void LeftJoin_ShouldReturnTwoTablesWithCorrectJoinType()
        {
            string sql = @"SELECT p.item_number AS item_number, u.keyed_name AS created_by
                        FROM part p
                        LEFT JOIN [user] u ON u.id = p.created_by_id";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result);
            Assert.Equal(2, result.Tables.Count);
            Assert.Equal("part", result.Tables[0].Name);
            Assert.Equal("user", result.Tables[1].Name);
            Assert.Equal("LeftOuter", result.Tables[1].JoinType);

            // parent-child relationship
            Assert.NotNull(result.Tables[1].ParentTable);
            Assert.Equal("part", result.Tables[1].ParentTable.Name);

            // table properties
            Assert.Single(result.Tables[0].Properties);
            Assert.Single(result.Tables[1].Properties);
            Assert.Equal("item_number", result.Tables[0].Properties[0].Name);
            Assert.Equal("keyed_name", result.Tables[1].Properties[0].Name);
        }

        [Fact]
        public void WhereClause_ShouldSetFilterOnTable()
        {
            string sql = @"SELECT p.item_number AS item_number
                        FROM part p
                        WHERE p.name = 'cats'";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result.Tables[0].WhereFilter);
            Assert.Contains("eq", result.Tables[0].WhereFilter);
            Assert.Contains("name", result.Tables[0].WhereFilter);
            Assert.Contains("cats", result.Tables[0].WhereFilter);
        }

        [Fact]
        public void SelectStar_ShouldReturnNull()
        {
            string sql = @"SELECT * FROM part p";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.Null(result); // should return NULL - SELECT * is not supported
        }

        [Fact]
        public void NonSelect_ShouldReturnNull()
        {
            string sql = "UPDATE part SET name = 'cat'";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.Null(result);
        }

        [Fact]
        public void NonSql_ShouldReturnNull()
        {
            string sql = "this is totally not sql";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.Null(result);
        }

        [Fact]
        public void EmptySql_ShouldReturnEmptyQueryDefinition()
        {
            string sql = "";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.Null(result.ContextItemID);
            Assert.Empty(result.WhereDefinitionList);
            Assert.Empty(result.Tables);
        }

        [Fact]
        public void ShouldStripInnovatorFromSql()
        {
            string sql = @"SELECT p.item_number AS item_number FROM innovator.part p";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result);
            Assert.Single(result.Tables);
            Assert.Equal("part", result.Tables[0].Name);
            Assert.Equal("p", result.Tables[0].Alias);
            Assert.Single(result.Tables[0].Properties);
            Assert.Equal("item_number", result.Tables[0].Properties[0].Name);
        }

        [Fact]
        public void SqlLikeStatement_ShouldAddLikeFilterOnTable()
        {
            string sql = @"SELECT p.item_number AS item_number
                            FROM part p
                            WHERE p.item_number LIKE 'PN-%'";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result);
            Assert.NotNull(result.Tables[0].WhereFilter);
            Assert.Contains("<property name=\"item_number\"", result.Tables[0].WhereFilter);
            Assert.Contains("<constant>PN-%</constant>", result.Tables[0].WhereFilter);
            Assert.Contains("like", result.Tables[0].WhereFilter);
        }

        [Fact]
        public void SqlMultipleWhereClauses_ShouldSetAllFiltersOnTable()
        {
            string sql = @"SELECT p.item_number AS item_number FROM part p
                            WHERE p.state = 'Released' AND p.generation = 1";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result.Tables[0].WhereFilter);
            Assert.Contains("<constant>Released</constant>", result.Tables[0].WhereFilter);
            Assert.Contains("<constant>1</constant>", result.Tables[0].WhereFilter);
            Assert.Contains("<property name=\"state\"", result.Tables[0].WhereFilter);
            Assert.Contains("<property name=\"generation\"", result.Tables[0].WhereFilter);
        }

        [Fact]
        public void InnerJoin_ShouldAddExistsFilterOnParentTable()
        {
            string sql = @"SELECT p.item_number AS item_number, d.name AS document_name
                   FROM part p
                   INNER JOIN document d ON d.id = p.source_id";
            QueryDefinition result = _converter.Generate(sql, new SqlToReportOptions { Name = "Test" });

            Assert.NotNull(result);
            Assert.Equal(2, result.Tables.Count);
            Assert.Equal("part", result.Tables[0].Name);
            Assert.Equal("document", result.Tables[1].Name);

            // Check join type
            Assert.Equal("Inner", result.Tables[1].JoinType);

            // Check parent/child relationship
            Assert.NotNull(result.Tables[1].ParentTable);
            Assert.Equal("part", result.Tables[1].ParentTable.Name);

            // INNER JOIN adds an exists condition on the parent table
            Assert.NotNull(result.Tables[0].WhereFilter);
            Assert.Contains("<exists>", result.Tables[0].WhereFilter);
        }

        [Fact]
        public void WriteQueryDefinition_ShouldReturnId()
        {
            var sql = @"SELECT p.item_number AS item_number
                FROM part p";
            var options = new SqlToReportOptions { Name = "Test", Title = "Test Report" };
            var qryDef = _converter.Generate(sql, options);

            Assert.NotNull(qryDef);

            string qryDefId = _converter.WriteQueryDefinition(_repository, qryDef, options);

            Assert.NotNull(qryDefId);
        }

        [Fact]
        public void WriteReportDefinition_ShouldReturnId()
        {
            var sql = @"SELECT p.item_number AS item_number
                FROM part p";
            var options = new SqlToReportOptions { Name = "Test", Title = "Test Report" };
            var qryDef = _converter.Generate(sql, options);
            string qryDefId = _converter.WriteQueryDefinition(_repository, qryDef, options);

            Assert.NotNull(qryDefId);

            string reportId = _converter.WriteReportDefinition(_repository, qryDefId, qryDef, options);

            Assert.NotNull(reportId);
        }
    }

}
