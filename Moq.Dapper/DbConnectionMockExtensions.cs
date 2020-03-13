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
    public static class DbConnectionMockExtensions
    {
        public static ISetup<TDbConnection, TResult> SetupDapper<TResult, TDbConnection>(this Mock<TDbConnection> mock, Expression<Func<TDbConnection, TResult>> expression)
            where TDbConnection : class, IDbConnection
        {
            var call = expression.Body as MethodCallExpression;

            if (call?.Method.DeclaringType != typeof(SqlMapper))
                throw new ArgumentException("Not a Dapper method.");

            switch (call.Method.Name)
            {
                case nameof(SqlMapper.ExecuteScalar):
                    return SetupExecuteScalar<TResult, TDbConnection>(mock);

                case nameof(SqlMapper.Query):
                case nameof(SqlMapper.QueryFirstOrDefault):
                    return SetupQuery<TResult, TDbConnection>(mock);

                default:
                    throw new NotSupportedException();
            }
        }

        public static ISetup<TDbConnection, int> SetupDapper<TDbConnection>(this Mock<TDbConnection> mock, Expression<Func<TDbConnection, int>> expression)
            where TDbConnection : class, IDbConnection
        {
            var call = expression.Body as MethodCallExpression;

            if (call?.Method.DeclaringType != typeof(SqlMapper))
                throw new ArgumentException("Not a Dapper method.");

            switch (call.Method.Name)
            {
                case nameof(SqlMapper.Execute):
                    return SetupExecute(mock);
                default:
                    return SetupDapper<int, TDbConnection>(mock, expression);
            }
        }

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
            return SetupExecuteCommandAsync<TResult, TDbConnection, int>(mock,
                (commandMock, result) => commandMock.Setup(x => x.ExecuteNonQueryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result));
        }

        static ISetup<TDbConnection, Task<TResult>> SetupQueryAsync<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommandAsync<TResult, TDbConnection>(mock, (commandMock, result) =>
            {
                commandMock.Protected()
                           .Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
                           .ReturnsAsync(() => DbDataReaderFactory.DbDataReader(result));
            });

        static ISetup<TDbConnection, Task<TResult>> SetupExecuteScalarAsync<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection
        {
            return SetupExecuteCommandAsync<TResult, TDbConnection, object>(mock,
                (commandMock, result) => commandMock.Setup(x => x.ExecuteScalarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(result));
        }

        static ISetup<TDbConnection, TResult> SetupQuery<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommand<TResult, TDbConnection>(mock, (commandMock, getResult) =>
                commandMock.Setup(command => command.ExecuteReader(It.IsAny<CommandBehavior>())).Returns(() => getResult().ToDataTable(typeof(TResult)).ToDataTableReader()));

        static ISetup<TDbConnection, TResult> SetupExecuteScalar<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommand<TResult, TDbConnection>(mock, (commandMock, result) => commandMock.Setup(command => command.ExecuteScalar()).Returns(() => result()));

        static ISetup<TDbConnection, int> SetupExecute<TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommand<int, TDbConnection>(mock, (commandMock, result) => commandMock.Setup(command => command.ExecuteNonQuery()).Returns(result));

        static ISetup<TDbConnection, Task<TResult>> SetupExecuteCommandAsync<TResult, TDbConnection, TMockResult>(
            Mock<TDbConnection> mock, Action<Mock<DbCommand>, Func<TMockResult>> mockResult)
             where TDbConnection : class, IDbConnection
        {
            var setupMock = new Mock<ISetup<TDbConnection, Task<TResult>>>();

            var result = default(TResult);

            setupMock.Setup(setup => setup.Returns(It.IsAny<Func<Task<TResult>>>()))
                     .Callback<Func<Task<TResult>>>(r => result = r().Result);

            var commandMock = new Mock<DbCommand>();

            commandMock.Protected()
                       .SetupGet<DbParameterCollection>("DbParameterCollection")
                       .Returns(new Mock<DbParameterCollection>().Object);

            commandMock.Protected()
                       .Setup<DbParameter>("CreateDbParameter")
                       .Returns(new Mock<DbParameter>().Object);

            mockResult(commandMock, () => (TMockResult)Convert.ChangeType(result, typeof(TMockResult)));

            mock.As<IDbConnection>()
                .Setup(m => m.CreateCommand())
                .Returns(commandMock.Object);

            mock.As<IDbConnection>()
                .SetupGet(m => m.State)
                .Returns(ConnectionState.Open);

            return setupMock.Object;
        }
    }
}
