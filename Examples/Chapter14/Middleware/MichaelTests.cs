using Dapper;
using Examples.Chapter2.DbLogger;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.SqlClient;

namespace Examples.Chapter14
{
    public static class MichaelTests
    {
        public static R Connect<R>(ConnectionString connString, Func<SqlConnection, R> f)
        {
            using var conn = new SqlConnection(connString);
            conn.Open();
            return f(conn);
        }

        public static void Log(string connString, LogMessage message)
           => Connect(connString, c => c.Execute("sp_create_log"
              , message, commandType: CommandType.StoredProcedure));

        public static T Trace<T>(ILogger log, string op, Func<T> f)
        {
            log.LogTrace($"Entering {op}");
            T t = f();
            log.LogTrace($"Leaving {op}");
            return t;
        }

        public static void Log(ILogger logger, string connString, LogMessage message)
           => Trace(logger, "CreateLog"
              , () => Connect(connString
                 , c => c.Execute("sp_create_log"
                    , message, commandType: CommandType.StoredProcedure)));

        public static R Connect<R>(string connString, Func<SqlConnection, R> func)
        {
            using var conn = new SqlConnection(connString);
            conn.Open();
            return func(conn);
        }

        public static R Transact<R>(SqlConnection conn, Func<SqlTransaction, R> f)
        {
            using var tran = conn.BeginTransaction();

            R r = f(tran);
            tran.Commit();

            return r;
        }

        public static Func<Func<SqlTransaction, dynamic>, dynamic> Bind(
            Func<Func<SqlConnection, dynamic>, dynamic> mw,
            Func<SqlConnection, Func<Func<SqlTransaction, dynamic>, dynamic>> f)
        {
            // the essense of middleware is to apply a continuation Func<T, dynamic> and get the value
            // this makes sure that continuation Func<SqlConnection, dynamic> is called within the scope of
            // continuation Func<SqlConnection, dynamic>
            return (Func<SqlTransaction, dynamic> innerContinuation) =>
            {
                Func<SqlConnection, dynamic> outerContinuation = (SqlConnection t) =>
                {
                    Func<Func<SqlTransaction, dynamic>, dynamic> mw2 = f(t);
                    dynamic ret = mw2(innerContinuation);
                    return ret;
                };
                dynamic ret = mw(outerContinuation);
                return ret;
            };
        }

        public static Func<Func<SqlTransaction, dynamic>, dynamic> Map(
            Func<Func<SqlConnection, dynamic>, dynamic> mw,
            Func<SqlConnection, SqlTransaction> f)
        {
            // the essense of middleware is to apply a continuation Func<T, dynamic> and get the value dynamic
            return (Func<SqlTransaction, dynamic> cont) =>
            {
                dynamic ret = mw((SqlConnection t) =>
                {
                    SqlTransaction trans = f(t);
                    dynamic ret = cont(trans);
                    return ret;
                });
                return ret;
            };
        }

        public static void Test()
        {
            Func<SqlConnection, int> continuation = connection =>
            {
                return connection.ExecuteScalar<int>("execute");
            };

            // the essense of middleware is given a continuation, it applies it and get result
            Func<Func<SqlConnection, int>, int> middleware = cont => Connect("connection string", cont);

            var result = middleware(continuation);

            var middleware2 = Connect("connection string").SelectMany(conn =>
            {
                return Transact(conn);
            }).Select(trans =>
            {
                return trans.Connection.ExecuteScalar<int>("select 100");
            });

            var result2 = middleware2.Run();
        }

        static Middleware<SqlConnection> Connect(ConnectionString connString)
            => f => ConnectionHelper.Connect(connString, f);

        static Middleware<SqlTransaction> Transact(SqlConnection conn)
            => f => ConnectionHelper.Transact(conn, f);
    }
}
