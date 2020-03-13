using System;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Moq.Language.Flow;
using Moq.Protected;
using static Moq.Dapper.DbCommandSetup;

namespace Moq.Dapper
{
    public static class DbConnectionAsyncMockExtensions
    {
        public static ISetup<TDbConnection, Task<TResult>> SetupDapperAsync<TResult, TDbConnection>(this Mock<TDbConnection> mock, Expression<Func<TDbConnection, Task<TResult>>> expression)
            where TDbConnection : class, IDbConnection
        {
            var call = expression.Body as MethodCallExpression;

            if (call?.Method.DeclaringType != typeof(SqlMapper))
                throw new ArgumentException("Not a Dapper method.");

            switch (call.Method.Name)
            {
                case nameof(SqlMapper.QueryAsync):
                    return SetupQueryAsync<TResult, TDbConnection>(mock);

                case nameof(SqlMapper.ExecuteScalarAsync):
                    return SetupExecuteScalarAsync<TResult, TDbConnection>(mock);

                case nameof(SqlMapper.ExecuteAsync):
                    return SetupExecuteAsync<TResult, TDbConnection>(mock);

                default:
                    throw new NotSupportedException();
            }
        }

        static ISetup<TDbConnection, Task<TResult>> SetupExecuteAsync<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection
        {
            return SetupCommandAsync<TResult, TDbConnection, int>(mock,
                (commandMock, result) => commandMock.Setup(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result));
        }

        static ISetup<TDbConnection, Task<TResult>> SetupQueryAsync<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommandAsync<TResult, TDbConnection, TResult>(mock, (commandMock, result) =>
            {
                commandMock.Protected()
                           .Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
                           .ReturnsAsync(() => DbDataReaderFactory.DbDataReader(result));
            });

        static ISetup<TDbConnection, Task<TResult>> SetupExecuteScalarAsync<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection
        {
            return SetupCommandAsync<TResult, TDbConnection, object>(mock,
                (commandMock, result) => commandMock.Setup(x => x.ExecuteScalarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result));
        }
    }
}
