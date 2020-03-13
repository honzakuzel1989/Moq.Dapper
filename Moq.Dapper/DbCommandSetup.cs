﻿using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Moq.Language.Flow;
using Moq.Protected;

namespace Moq.Dapper
{
    public static class DbCommandSetup
    {
        internal static ISetup<TDbConnection, Task<TResult>> SetupCommandAsync<TResult, TDbConnection, TMockResult>(
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

            mockResult(commandMock, () => typeof(TMockResult) == typeof(TResult) ?
                (TMockResult)(object)result : (TMockResult)Convert.ChangeType(result, typeof(TMockResult)));

            var connnMock = mock.As<IDbConnection>();

            connnMock.Setup(m => m.CreateCommand())
                             .Returns(commandMock.Object);

            connnMock.SetupGet(m => m.State).Returns(ConnectionState.Open);

            return setupMock.Object;
        }

        internal static ISetup<TDbConnection, TResult> SetupCommand<TResult, TDbConnection>(
            Mock<TDbConnection> mock, Action<Mock<IDbCommand>, Func<TResult>> mockResult)
            where TDbConnection : class, IDbConnection
        {
            var setupMock = new Mock<ISetup<TDbConnection, TResult>>();
            var returnsMock = new Mock<IReturnsResult<TDbConnection>>();

            Func<TResult> getResult = null;
            Action callback = null;

            setupMock.Setup(setup => setup.Returns(It.IsAny<Func<TResult>>()))
                     .Returns(returnsMock.Object)
                     .Callback<Func<TResult>>(r => getResult = r);

            setupMock.Setup(setup => setup.Returns(It.IsAny<TResult>()))
                     .Returns(returnsMock.Object)
                     .Callback<TResult>(r => getResult = () => r);

            returnsMock.Setup(rm => rm.Callback(It.IsAny<Action>()))
                       .Callback<Action>(a => callback = a);

            var commandMock = new Mock<IDbCommand>();

            commandMock.SetupGet(a => a.Parameters)
                       .Returns(new Mock<IDataParameterCollection>().Object);

            commandMock.Setup(a => a.CreateParameter())
                       .Returns(new Mock<IDbDataParameter>().Object);

            mockResult(commandMock, () =>
            {
                var result = getResult();
                callback?.Invoke();
                return result;
            });

            mock.Setup(connection => connection.CreateCommand())
                .Returns(commandMock.Object);

            return setupMock.Object;
        }
    }
}