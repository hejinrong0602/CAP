﻿using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using DotNetCore.CAP.Abstractions;
using DotNetCore.CAP.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace DotNetCore.CAP.MySql
{
    public class CapPublisher : CapPublisherBase, ICallbackPublisher
    {
        private readonly DbContext _dbContext;
        private readonly ILogger _logger;
        private readonly MySqlOptions _options;

        public CapPublisher(IServiceProvider provider,
            ILogger<CapPublisher> logger,
            MySqlOptions options)
        {
            ServiceProvider = provider;
            _options = options;
            _logger = logger;

            if (_options.DbContextType == null) return;
            IsUsingEF = true;
            _dbContext = (DbContext) ServiceProvider.GetService(_options.DbContextType);
        }

        public async Task PublishAsync(CapPublishedMessage message)
        {
            using (var conn = new MySqlConnection(_options.ConnectionString))
            {
                await conn.ExecuteAsync(PrepareSql(), message);
            }
        }

        protected override void PrepareConnectionForEF()
        {
            DbConnection = _dbContext.Database.GetDbConnection();
            var dbContextTransaction = _dbContext.Database.CurrentTransaction;
            var dbTrans = dbContextTransaction?.GetDbTransaction();
            //DbTransaction is dispose in original
            if (dbTrans?.Connection == null)
            {
                IsCapOpenedTrans = true;
                dbContextTransaction?.Dispose();
                dbContextTransaction = _dbContext.Database.BeginTransaction(IsolationLevel.ReadCommitted);
                dbTrans = dbContextTransaction.GetDbTransaction();
            }
            DbTransaction = dbTrans;
        }

        protected override void Execute(IDbConnection dbConnection, IDbTransaction dbTransaction,
            CapPublishedMessage message)
        {
            dbConnection.Execute(PrepareSql(), message, dbTransaction);

            _logger.LogInformation("Published Message has been persisted in the database. name:" + message);
        }

        protected override async Task ExecuteAsync(IDbConnection dbConnection, IDbTransaction dbTransaction,
            CapPublishedMessage message)
        {
            await dbConnection.ExecuteAsync(PrepareSql(), message, dbTransaction);

            _logger.LogInformation("Published Message has been persisted in the database. name:" + message);
        }

        #region private methods

        private string PrepareSql()
        {
            return
                $"INSERT INTO `{_options.TableNamePrefix}.published` (`Name`,`Content`,`Retries`,`Added`,`ExpiresAt`,`StatusName`)VALUES(@Name,@Content,@Retries,@Added,@ExpiresAt,@StatusName)";
        }

        #endregion private methods
    }
}