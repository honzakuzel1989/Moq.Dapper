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

                case nameof(SqlMapper.Execute):
                    return SetupExecute<TResult, TDbConnection>(mock);

                default:
                    throw new NotSupportedException();
            }
        }

        static ISetup<TDbConnection, TResult> SetupQuery<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommand<TResult, TDbConnection>(mock, (commandMock, getResult) =>
                commandMock.Setup(command => command.ExecuteReader(It.IsAny<CommandBehavior>())).Returns(() => getResult().ToDataTable(typeof(TResult)).ToDataTableReader()));

        static ISetup<TDbConnection, TResult> SetupExecuteScalar<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommand<TResult, TDbConnection>(mock, (commandMock, result) => commandMock.Setup(command => command.ExecuteScalar()).Returns(() => result()));

        static ISetup<TDbConnection, TResult> SetupExecute<TResult, TDbConnection>(Mock<TDbConnection> mock) where TDbConnection : class, IDbConnection =>
            SetupCommand<TResult, TDbConnection>(mock, (commandMock, result) => commandMock.Setup(command => command.ExecuteNonQuery()).Returns(result));
    }
}
