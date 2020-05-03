using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;

namespace BbcCorp.Neo4j
{
    public interface INeoGraphManager
    {
        Task ExecuteNonQuery(string cypherQuery, object queryParams=null);
        Task<T> ExecuteScalar<T>(string cypherQuery, object queryParams=null);

        Task<List<T>> FetchRecords<T>(Func<IRecord, T> recordProcessor, string cypherQuery, object queryParams=null);

        IAsyncEnumerable<List<T>> FetchRecordsAsStream<T>(Func<IRecord, T> recordProcessor, string cypherQuery, object queryParams = null, long bufferSize = 100);
    
    }
}