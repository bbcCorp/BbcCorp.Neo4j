using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace BbcCorp.Neo4j
{
    public class NeoGraphManager : INeoGraphManager, IDisposable
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly IDriver _driver;
        private readonly string _database;

        public NeoGraphManager(ILogger<NeoGraphManager> logger, String server, String user, String password, string database="neo4j", int port=7687)
        {
            var uri = $"bolt://{server}:{port}";

            _logger = logger;
            
            _driver = CreateDriverWithCustomizedConnectionPool(uri, user, password);

            _database = database;
        }

        public IDriver CreateDriverWithCustomizedConnectionPool(string uri, string user, string password, 
            int maxConnectionLifetime=30, // 30 minutes
            int maxConnectionPoolSize=50,
            int connectionAcquisitionTimeout = 2, // 2 minutes
            int maxTransactionRetryTime = 15 // seconds
            )
        {
            return GraphDatabase.Driver(uri, AuthTokens.Basic(user, password),
                o => o
                    .WithEncryptionLevel(EncryptionLevel.None)
                    .WithMaxTransactionRetryTime(TimeSpan.FromSeconds(maxTransactionRetryTime))
                    .WithMaxConnectionLifetime(TimeSpan.FromMinutes(maxConnectionLifetime))
                    .WithMaxConnectionPoolSize(maxConnectionPoolSize)
                    .WithConnectionAcquisitionTimeout(TimeSpan.FromMinutes(connectionAcquisitionTimeout)));
        }

        public async Task ExecuteNonQuery(string cypherQuery, object queryParams=null)
        {
            IAsyncSession session = _driver.AsyncSession(o => o.WithDatabase(this._database));

            if(queryParams == null)
            {
                queryParams= new {};
            }

            try
            {
                _logger.LogDebug($"Executing query: {cypherQuery}");

                IResultCursor cursor = await session.RunAsync(cypherQuery, queryParams);

                IResultSummary result = await cursor.ConsumeAsync();

                _logger.LogTrace($"Query executed successfully.");
                
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error executing query. {ex.Message}");
                throw;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        public async Task<T> ExecuteScalar<T>(string cypherQuery, object queryParams=null)
        {
            T result = default(T);
            IAsyncSession session = _driver.AsyncSession(o => o.WithDatabase(this._database));

            _logger.LogDebug($"Executing query: {cypherQuery}");
            
            if(queryParams == null)
            {
                queryParams= new {};
            }

            try
            {     

                IResultCursor resultCursor = await session.RunAsync(cypherQuery, queryParams);

                IRecord record = await resultCursor.SingleAsync();

                result = record[0].As<T>();

                _logger.LogDebug("Query executed successfully");
                
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error executing query. {ex.Message}");
                throw;
            }
            finally
            {
                await session.CloseAsync();
                
            }

            return result;
        }

        // Get all records as a List
        public async Task<List<T>> FetchRecords<T>(
            Func<IRecord, T> recordProcessor, 
            string cypherQuery, 
            object queryParams=null)
        {
            List<T> result = null;
            IAsyncSession session = _driver.AsyncSession(o => o.WithDatabase(this._database));

            _logger.LogDebug($"Executing query: {cypherQuery}");
            
            if(queryParams == null)
            {
                queryParams= new {};
            }

            try
            {     

                IResultCursor resultCursor = await session.RunAsync(cypherQuery, queryParams);

                result = await resultCursor.ToListAsync(record => recordProcessor(record));

                _logger.LogDebug("Query executed successfully");
                
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Error executing query. {ex.Message}");
                throw;
            }
            finally
            {
                await session.CloseAsync();
                
            }

            return result;
        }

        // Get records as a stream of buffered List
        public async IAsyncEnumerable<List<T>> FetchRecordsAsStream<T>(
            Func<IRecord, T> recordProcessor, 
            string cypherQuery, object queryParams=null, 
            long bufferSize=100)
        {
            long recordsProcessed = 0;

            List<T> resultBuffer = new List<T>();
            IAsyncSession session = _driver.AsyncSession(o => o.WithDatabase(this._database));

            _logger.LogDebug($"Executing query: {cypherQuery}");
            
            if(queryParams == null)
            {
                queryParams= new {};
            }

            try
            {     

                IResultCursor resultCursor = await session.RunAsync(cypherQuery, queryParams);

                _logger.LogDebug("Reading cursor");

                while (await resultCursor.FetchAsync())
                {
                    recordsProcessed += 1;

                    resultBuffer.Add( recordProcessor(resultCursor.Current));
                    
                    if(resultBuffer.Count >= bufferSize)
                    {
                        _logger.LogDebug($"Records processed: {recordsProcessed} ...");
                        yield return resultBuffer;
                        resultBuffer.Clear();
                    }
                }

                _logger.LogDebug($"* Total records processed: {recordsProcessed} *");
                yield return resultBuffer;
                                            
            }
            finally
            {
                await session.CloseAsync();
                
            }
        }


        public async Task createIndex(String nodeLabel, String field)
        {
            _logger.LogDebug($"Creating index on {nodeLabel}:{field}");

            var query = $"CREATE INDEX ON :{nodeLabel}({field})";

            await this.ExecuteNonQuery(query);

            _logger.LogInformation($"Created index on {nodeLabel}:{field}");
        }

        public async Task dropIndex(String nodeLabel, String field)
        {
            _logger.LogDebug($"Dropping index on {nodeLabel}:{field}");

            var query = $"DROP INDEX ON :{nodeLabel}({field})";

            await this.ExecuteNonQuery(query);

            _logger.LogInformation($"Dropped index on {nodeLabel}:{field}");
        }


        public async Task createUniqueConstraint(String nodeLabel, String field)
        {
            _logger.LogDebug($"Creating unique constraint on {nodeLabel}:{field}");

            var query = $"CREATE CONSTRAINT ON (n:{nodeLabel}) ASSERT n.{field} IS UNIQUE";

            await this.ExecuteNonQuery(query);

            _logger.LogInformation($"Created unique constraint on {nodeLabel}:{field}");
        }



        public void Dispose()
        {
            _driver?.Dispose();
        }
    }
}
