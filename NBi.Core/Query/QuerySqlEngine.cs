﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;

namespace NBi.Core.Query
{
    /// <summary>
    /// Engine wrapping the System.Data.SqlClient namespace for execution of NBi tests
    /// <remarks>Instances of this class are built by the means of the <see>QueryEngineFactory</see></remarks>
    /// </summary>
    internal class QuerySqlEngine : IQueryExecutor, IQueryPerformance, IQueryParser, IQueryEnginable, IQueryFormat
    {
        protected readonly SqlCommand command;


        protected internal QuerySqlEngine(SqlCommand command)
        {
            this.command = command;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public void CleanCache()
        {
            
            using (var conn = new SqlConnection(command.Connection.ConnectionString))
            {
                var clearSql = new string[] { "dbcc freeproccache", "dbcc dropcleanbuffers" };

                conn.Open();

                foreach (var sql in clearSql)
                {
                    using (SqlCommand cleanCmd = new SqlCommand(sql, conn))
                    {
                        cleanCmd.ExecuteNonQuery();
                    }
                }
            }
            
        }

        public PerformanceResult CheckPerformance()
        {
            return CheckPerformance(0);
        }

        public PerformanceResult CheckPerformance(int timeout)
        {
            bool isTimeout=false;
            DateTime tsStart, tsStop = DateTime.Now;

            if (command.Connection.State == ConnectionState.Closed)
            {
                try
                {
                    command.Connection.Open();
                }
                catch (SqlException ex)
                {
                    throw new ConnectionException(ex, command.Connection.ConnectionString);
                }
            }

            Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, command.CommandText);
            foreach (SqlParameter param in command.Parameters)
                Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, string.Format("{0} => {1}", param.ParameterName, param.Value));

            command.CommandTimeout = timeout / 1000;

            tsStart = DateTime.Now;
            try
            {
                command.ExecuteNonQuery();
                tsStop = DateTime.Now;
            }
            catch (SqlException e)
            {
                if (!e.Message.StartsWith("Timeout expired.") && !e.Message.StartsWith("Execution Timeout Expired."))
                    throw;
                isTimeout=true;
            }
            

            if (command.Connection.State == ConnectionState.Open)
                command.Connection.Close();

            if (isTimeout)
                return PerformanceResult.Timeout(timeout);
            else
                return new PerformanceResult(tsStop.Subtract(tsStart));

        }

        /// <summary>
        /// Method exposed by the interface IQueryParser to execute a test of syntax
        /// </summary>
        /// <remarks>This method makes usage the set statement named SET FMTONLY to not effectively execute the query but check the validity of this query</remarks>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public ParserResult Parse()
        {
            ParserResult res = null;

            using (var conn = new SqlConnection(command.Connection.ConnectionString))
            {
                Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, command.CommandText);
                foreach (SqlParameter param in command.Parameters)
                    Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, string.Format("{0} => {1}", param.ParameterName, param.Value));

                var fullSql = string.Format(@"SET FMTONLY ON; {0} SET FMTONLY OFF;", command.CommandText);

                try
                {
                    conn.Open();
                }
                catch (SqlException ex)
                {
                    throw new ConnectionException(ex, command.Connection.ConnectionString);
                }
                

                using (SqlCommand cmdIn = new SqlCommand(fullSql, conn))
                {
                    try
                    {
                        cmdIn.ExecuteNonQuery();
                        res = ParserResult.NoParsingError();
                    }
                    catch (SqlException ex)
                    {
                        res = new ParserResult(ex.Message.Split(new string[] { "\r\n" }, System.StringSplitOptions.RemoveEmptyEntries));
                    }

                }

                if (conn.State != System.Data.ConnectionState.Closed)
                    conn.Close();
            }

            return res;
        }

        public DataSet Execute()
        {
            float i;
            return Execute(out i);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities")]
        public DataSet Execute(out float elapsedSec)
        {
            // Open the connection
            using (var connection = new SqlConnection())
            {
                var connectionString = command.Connection.ConnectionString;
                try
                { connection.ConnectionString = connectionString; }
                catch (ArgumentException ex)
                { throw new ConnectionException(ex, connectionString); }
                try
                    {connection.Open();}
                catch (SqlException ex)
                { throw new ConnectionException(ex, connectionString); }

                Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, command.CommandText);
                foreach (SqlParameter param in command.Parameters)
                    Trace.WriteLineIf(NBiTraceSwitch.TraceVerbose, string.Format("{0} => {1}", param.ParameterName, param.Value));

                // capture time before execution
                DateTime timeBefore = DateTime.Now;
                var adapter = new SqlDataAdapter(command);
                var ds = new DataSet();

                try
                {
                    adapter.Fill(ds);
                }
                catch (SqlException ex)
                {
                    if (ex.Number == -2)
                        throw new CommandTimeoutException(ex, adapter.SelectCommand);
                    throw;
                }
                

                // capture time after execution
                DateTime timeAfter = DateTime.Now;

                // setting query runtime
                elapsedSec = (float)timeAfter.Subtract(timeBefore).TotalSeconds;
                Trace.WriteLineIf(NBiTraceSwitch.TraceInfo, string.Format("Time needed to execute query: {0}", timeAfter.Subtract(timeBefore).ToString(@"d\d\.hh\h\:mm\m\:ss\s\ \+fff\m\s")));

                return ds;
            }
        }

        public FormattedResults GetFormats()
        {
            var dataSet = Execute();
            var formattedResults = new FormattedResults();

            foreach (DataRow row in dataSet.Tables[0].Rows)
            {
                foreach (var item in row.ItemArray)
                    formattedResults.Add(item.ToString());
            }
            return formattedResults;
        }
    }
}
