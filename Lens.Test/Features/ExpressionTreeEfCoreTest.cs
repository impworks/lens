#if !NET_CLASSIC

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Phase 5, end to end: the trees LENS builds are handed to a real query provider and have to
    /// come out as SQL. Asserting on the SQL is what tells apart a query that ran in the database
    /// from one that pulled the table into memory and filtered it there - the failure the whole
    /// phase exists to prevent, and the one that looks like a performance problem rather than a
    /// wrong answer.
    ///
    /// EF Core has no .NET Framework leg, so this fixture is compiled out of the net47 build.
    /// </summary>
    [TestFixture]
    internal class ExpressionTreeEfCoreTest : TestBase
    {
        #region Tests

        [Test]
        public void PredicateBecomesAWhereClause()
        {
            var sql = Sql(@"db.People
    |> Where p -> p.Age > 18");

            Assert.IsTrue(sql.Contains("WHERE"), sql);
            Assert.IsTrue(sql.Contains("> 18"), sql);
        }

        [Test]
        public void ConjunctionBecomesOneWhereClause()
        {
            var sql = Sql(@"db.People
    |> Where p -> p.Age > 18 && p.Name <> ""bob""");

            Assert.IsTrue(sql.Contains("AND"), sql);
        }

        [Test]
        public void ProjectionBecomesASelectList()
        {
            var sql = Sql(@"db.People
    |> Where p -> p.Age > 18
    |> Select p -> p.Name");

            Assert.IsTrue(sql.Contains("WHERE"), sql);
            Assert.IsFalse(sql.Contains("\"p\".\"Age\","), sql);
        }

        [Test]
        public void OrderingBecomesAnOrderByClause()
        {
            var sql = Sql(@"db.People
    |> OrderBy p -> p.Name");

            Assert.IsTrue(sql.Contains("ORDER BY"), sql);
        }

        [Test]
        public void MethodCallBecomesALikeClause()
        {
            var sql = Sql(@"db.People
    |> Where p -> p.Name.StartsWith ""a""");

            Assert.IsTrue(sql.Contains("LIKE"), sql);
        }

        [Test]
        public void CapturedLocalBecomesAParameter()
        {
            // a closure field reads as a parameter rather than a literal, which is what lets the
            // provider reuse the query plan
            var sql = Sql(@"var limit = 18
db.People
    |> Where p -> p.Age > limit");

            Assert.IsTrue(sql.Contains("WHERE"), sql);
            Assert.IsFalse(sql.Contains("> 18"), sql);
        }

        [Test]
        public void QueryReturnsTheRightRows()
        {
            using var context = CreateContext();

            var names = (string[]) Run(context, @"db.People
    |> Where p -> p.Age > 18
    |> OrderBy p -> p.Name
    |> Select p -> p.Name
    |> ToArray ()");

            Assert.AreEqual(new[] {"alice", "carol"}, names);
        }

        #endregion

        #region Helpers

        private static string Sql(string src)
        {
            using var context = CreateContext();

            var query = (IQueryable) Run(context, src);
            return query.ToQueryString();
        }

        private static object Run(PeopleContext context, string src)
        {
            var compiler = CreateCompiler(new LensCompilerOptions {UnrollConstants = true});
            compiler.RegisterProperty("db", () => context);
            return compiler.Run(src);
        }

        private static PeopleContext CreateContext()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var context = new PeopleContext(connection);
            context.Database.EnsureCreated();

            context.People.AddRange(
                new Person {Id = 1, Name = "alice", Age = 30},
                new Person {Id = 2, Name = "bob", Age = 10},
                new Person {Id = 3, Name = "carol", Age = 40}
            );
            context.SaveChanges();

            return context;
        }

        #endregion
    }

    public class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class PeopleContext : DbContext
    {
        private readonly DbConnection _connection;

        public PeopleContext(DbConnection connection)
        {
            _connection = connection;
        }

        public DbSet<Person> People { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite(_connection);
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection.Dispose();
        }
    }
}

#endif
