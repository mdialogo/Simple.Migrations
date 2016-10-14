﻿using System;
using System.Data;

namespace SimpleMigrations
{
    /// <summary>
    /// Version provider which acts by maintaining a table of applied versions
    /// </summary>
    public abstract class VersionProviderBase : IVersionProvider<IDbConnection>
    {
        /// <summary>
        /// Ensure that the version table exists, creating it if necessary
        /// </summary>
        /// <param name="connection">Connection to use to perform this action</param>
        public virtual void EnsureCreated(IDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            this.RunInTransaction(connection, command =>
            {
                command.CommandText = this.GetCreateVersionTableSql();
                command.ExecuteNonQuery();
            });
        }

        /// <summary>
        /// Return the current version from the version table
        /// </summary>
        /// <param name="connection">Connection to use to perform this action</param>
        /// <returns>Current version</returns>
        public virtual long GetCurrentVersion(IDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = this.GetCurrentVersionSql();
                var result = cmd.ExecuteScalar();

                if (result == null)
                    return 0;

                long version;
                try
                {
                    version = Convert.ToInt64(result);
                }
                catch
                {
                    throw new MigrationException("Version Provider returns a value for the current version which isn't a long");
                }   

                return version;
            }
        }

        /// <summary>
        /// Upgrade the current version in the version table
        /// </summary>
        /// <param name="connection">Connection to use to perform this action</param>
        /// <param name="oldVersion">Version being upgraded from</param>
        /// <param name="newVersion">Version being upgraded to</param>
        /// <param name="newDescription">Description to associate with the new version</param>
        public virtual void UpdateVersion(IDbConnection connection, long oldVersion, long newVersion, string newDescription)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            this.RunInTransaction(connection, command =>
            {
                command.CommandText = this.GetSetVersionSql();

                var versionParam = command.CreateParameter();
                versionParam.ParameterName = "Version";
                versionParam.Value = newVersion;
                command.Parameters.Add(versionParam);

                var nameParam = command.CreateParameter();
                nameParam.ParameterName = "Description";
                nameParam.Value = newDescription;
                command.Parameters.Add(nameParam);

                command.ExecuteNonQuery();
            });
        }

        /// <summary>
        /// Runs the given action in a transaction
        /// </summary>
        /// <param name="connection">Connection to open the transaction on</param>
        /// <param name="action">Action to be executed. This takes a command, which already has the transaction assigned</param>
        protected virtual void RunInTransaction(IDbConnection connection, Action<IDbCommand> action)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        action(command);
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// Should 'CREATE TABLE IF NOT EXISTS', or similar
        /// </summary>
        public abstract string GetCreateVersionTableSql();

        /// <summary>
        /// Should return SQL which selects a single long value - the current version - or 0/NULL if there is no current version
        /// </summary>
        /// <returns></returns>
        public abstract string GetCurrentVersionSql();

        /// <summary>
        /// Returns SQL which upgrades to a particular version.
        /// </summary>
        /// <remarks>
        /// The following parameters should be used:
        ///  - @Version - the long version to set
        ///  - @Description - the description of the version
        /// </remarks>
        /// <returns></returns>
        public abstract string GetSetVersionSql();
    }
}
