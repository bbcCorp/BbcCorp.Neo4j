using System;
using Xunit;
using Moq;
using BbcCorp.Neo4j;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using Neo4j.Driver;
using System.Linq;

namespace BbcCorp.Neo4j.Tests
{
    public class NeoGraphManagerIntegrationTests : TestBase, IAsyncDisposable
    {
        private readonly string INTEGRATION_TESTNODE_LABEL;
        private readonly INeoGraphManager gm;

        private readonly string resetDbQuery;
        private readonly string nodeCountQuery;

        public NeoGraphManagerIntegrationTests()
        {
            this.INTEGRATION_TESTNODE_LABEL = $"NodeGraphManager_{Guid.NewGuid().ToString("N")}";

            Console.WriteLine($"Using Neo4j Server {Configuration["NEO4J_SERVER"]}:{Configuration["NEO4J_PORT"]} as {Configuration["NEO4J_DB_USER"]}/{Configuration["NEO4J_DB_PWD"]}");

            this.gm = new NeoGraphManager(
                logger: loggerFactory.CreateLogger<NeoGraphManager>(),
                server: Configuration["NEO4J_SERVER"],
                port: Convert.ToInt16(Configuration["NEO4J_PORT"]),
                user: Configuration["NEO4J_DB_USER"],
                password: Configuration["NEO4J_DB_PWD"]);


            // Clear all {INTEGRATION_TESTNODE_LABEL} nodes
            resetDbQuery = $"MATCH (n:{INTEGRATION_TESTNODE_LABEL}) DELETE n ";
            nodeCountQuery = $"MATCH (n:{INTEGRATION_TESTNODE_LABEL}) return COUNT(n)";
        }

        public async ValueTask DisposeAsync()
        {
            // ... clean up test data from the database ...

            // Remove all {INTEGRATION_TESTNODE_LABEL} nodes
            await gm.ExecuteNonQuery(resetDbQuery);

        }

        private async Task CreateNNodes(int count)
        {
            var query = $"CREATE (a:{INTEGRATION_TESTNODE_LABEL}" +
                "{message: $message, createdBy: $user, createdOn: TIMESTAMP(), recordid:$recordid }) ";

            for (int i=0; i<count; i++)
            {
                await gm.ExecuteNonQuery(query, new { message = $"test node {i}", user = "bbc", recordid = i });
            }
        }

        [Fact]
        public async Task SimpleNodeTests()
        {
            
            await gm.ExecuteNonQuery(resetDbQuery);
            //Console.WriteLine($"Removed all nodes with label:{INTEGRATION_TESTNODE_LABEL}");

            
            var count = await gm.ExecuteScalar<int>(nodeCountQuery);
            Assert.Equal(0, count);

            // Create a greeting node
            var query = $"CREATE (a:{INTEGRATION_TESTNODE_LABEL}" +
                "{message: $message, createdBy: $user, createdOn: TIMESTAMP() }) ";
            await gm.ExecuteNonQuery(query, new { message = "hello, world", user = "bbc" });

            // Update the greeting node
            query = $"MERGE (a:{INTEGRATION_TESTNODE_LABEL}) " +
                " ON CREATE SET a.createdOn= TIMESTAMP(), a.createdBy=$user " +
                " ON  MATCH SET a.updatedOn= TIMESTAMP(), a.updatedBy=$user " +
                " SET a.message= $message " +
                " RETURN a.message + ', from node ' + id(a)";

            var greeting = await gm.ExecuteScalar<String>(query, new { message = "hello, world", user = "bbc" });
            Assert.StartsWith("hello, world", greeting);

            // Get count of the greeting node
            Assert.Equal(1, await gm.ExecuteScalar<int>(nodeCountQuery));

            query = $"CREATE (n:{INTEGRATION_TESTNODE_LABEL}" +
                "{message: $message, createdBy: $user, createdOn: TIMESTAMP() }) return id(n)";
            var nodeid = await gm.ExecuteScalar<int>(query, new { message = "hello, bbc", user = "bbc1" });
            Assert.True(nodeid > 0);
            Assert.Equal(2, await gm.ExecuteScalar<int>(nodeCountQuery));

            // Get count of the greeting node created by a certain user
            query = $"MATCH (n:{INTEGRATION_TESTNODE_LABEL}) WHERE n.createdBy =~'(?i)bbc.*' return COUNT(n)";
            count = await gm.ExecuteScalar<int>(query);
            Assert.Equal(2, count);


            // Get all messages of type:INTEGRATION_TESTNODE_LABEL as a List of Tuples
            //Console.WriteLine($"Fetching all messages");
            query = $"MATCH (n:{INTEGRATION_TESTNODE_LABEL}) return id(n) as id, n.message as msg";
            var messages = await gm.FetchRecords<Tuple<int, string>>(
                cypherQuery: query,
                queryParams: new { },
                recordProcessor: record => Tuple.Create(record["id"].As<int>(), record["msg"].As<string>())
            );
            Assert.Equal(2, messages.Count);
            Assert.Equal(2, messages.Where(m => m.Item2.Contains("hello")).Count());

            // Get all messages of type:INTEGRATION_TESTNODE_LABEL as a stream of buffered List of Tuples
            await CreateNNodes(20);
            Assert.Equal(22, await gm.ExecuteScalar<int>(nodeCountQuery));

            query = $"MATCH (n:{INTEGRATION_TESTNODE_LABEL}) return id(n) as id, n.message as msg";
            int bufferSize = 10;

            var fetchRecordBuffer = gm.FetchRecordsAsStream<Tuple<int, string>>(
                cypherQuery: query,
                bufferSize: bufferSize,
                queryParams: new { },
                recordProcessor: record => Tuple.Create(record["id"].As<int>(), record["msg"].As<string>())
            );

            int totalRecsProcessed = 0;
            await foreach (var messageBuffer in fetchRecordBuffer)
            {
                Assert.InRange(messageBuffer.Count, 0, bufferSize);

                foreach (var msg in messageBuffer)
                {
                    totalRecsProcessed += 1;
                    //Console.WriteLine($" node#{msg.Item1}: {msg.Item2}");
                }

                //Console.WriteLine($"Fetching next set of {bufferSize} records ...");
            }
            Assert.Equal(22, totalRecsProcessed);

            
        }

        [Fact]
        public async Task ManageIndexTests()
        {

            // Create a greeting node
            var query = $"CREATE (a:{INTEGRATION_TESTNODE_LABEL}" +
                "{message: $message, createdBy: $user, createdOn: TIMESTAMP() }) ";
            await gm.ExecuteNonQuery(query, new { message = "hello, world", user = "bbc" });

            await gm.createIndex(nodeLabel:INTEGRATION_TESTNODE_LABEL, field:"message");

            await CreateNNodes(4);
            Assert.Equal(5, await gm.ExecuteScalar<int>(nodeCountQuery));

            await gm.dropIndex(nodeLabel: INTEGRATION_TESTNODE_LABEL, field: "message");

            // Remove all {INTEGRATION_TESTNODE_LABEL} nodes
            await gm.ExecuteNonQuery(resetDbQuery);

        }
    }
}
