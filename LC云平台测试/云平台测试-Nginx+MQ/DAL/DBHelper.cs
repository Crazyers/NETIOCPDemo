using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace AsyncSocketServer
{
    public class DBHelper
    {
        private static SqlCommand com;
        private static SqlDataReader reader;
        private static SqlDataAdapter adapter;
        private static SqlConnection conn;

        public static SqlConnection NewConn
        {
            get
            {
                string connectionString = "";
                connectionString = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings.Settings["ConnectionString"].Value;
                //应该在这里先判断conn是否为Nothing
                if (conn == null)
                {
                    conn = new SqlConnection(connectionString);
                }
                if (conn.State != ConnectionState.Open)
                {
                    conn.Open();
                }
                return conn;
            }
        }

        //执行增删改(无参)
        public static int ExecuteNonQuery(string sql)
        {
            com = new SqlCommand(sql, NewConn);
            return com.ExecuteNonQuery();
        }

        //执行增删改(有参)
        public static int ExecuteNonQuery(string sql, SqlParameter[] para)
        {
            com = new SqlCommand(sql, NewConn);
            com.Parameters.AddRange(para);
            return com.ExecuteNonQuery();
        }

        //执行增删改的存储过程
        public static int ExecuteNonQuery(SqlParameter[] para, string ProcedureName)
        {
            SqlCommand cmd = default(SqlCommand);
            cmd = new SqlCommand();
            cmd.Connection = NewConn;
            cmd.CommandText = ProcedureName;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddRange(para);
            return com.ExecuteNonQuery();
        }

        //执行查询(返回一个结果集，无参)
        public static int GetScalar(string sql)
        {
            com = new SqlCommand(sql, NewConn);
            return Convert.ToInt32(com.ExecuteScalar());
        }

        //执行查询(返回一个结果集，有参)
        public static int GetScalar(string sql, SqlParameter[] para)
        {
            com = new SqlCommand(sql, NewConn);
            com.Parameters.AddRange(para);
            return Convert.ToInt32(com.ExecuteScalar());
        }
        //执行查询(返回一行数据,无参)

        public static SqlDataReader GetReader(string sql)
        {
            com = new SqlCommand(sql, NewConn);
            reader = com.ExecuteReader();
            return reader;
        }
        //执行查询(返回一行数据,有参)

        public static SqlDataReader GetReader(string sql, SqlParameter[] para)
        {
            com = new SqlCommand(sql, NewConn);
            com.Parameters.AddRange(para);
            reader = com.ExecuteReader();
            return reader;
        }
        //执行查询(返回一个数据集,无参)

        public static DataTable GetDataTable(string sql)
        {
            DataSet dataset = default(DataSet);
            dataset = new DataSet();
            com = new SqlCommand(sql, NewConn);
            adapter = new SqlDataAdapter(com);
            adapter.Fill(dataset);
            return dataset.Tables[0];
        }
        //执行查询(返回一个数据集,有参)
        public static DataTable GetDataTable(string sql, SqlParameter[] para)
        {
            DataSet dataset = default(DataSet);
            dataset = new DataSet();
            com = new SqlCommand(sql, NewConn);
            com.Parameters.AddRange(para);
            adapter = new SqlDataAdapter(com);
            adapter.Fill(dataset);
            return dataset.Tables[0];
        }
    }
}
