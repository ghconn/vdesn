using System;
using System.Text;
using System.Linq;
using Chloe;
using Chloe.SqlServer;
using Chloe.MySql;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using desn.mdl;

namespace desn
{
    class Program
    {
        private static IConfiguration appConfiguration;
        static void Main(string[] args)
        {
            new HostBuilder().ConfigureAppConfiguration((context, builder) =>
            {
                var hostingEnviroment = context.HostingEnvironment;
                appConfiguration = AppConfigurations.Get(hostingEnviroment.ContentRootPath, hostingEnviroment.EnvironmentName);
            })
            .Build();

            var arg = args.Length > 0 ? args[0] : "";
            var arg_prefix = args.Length > 1 ? args[1] : "";

            if (arg.ToLower() == "mysql")
            {
                MysqlTableDefineToHTML(arg_prefix);
            }
            else
            {
                SqlServerTableDefineToHTML(arg_prefix);
            }
            
            Console.WriteLine("done.");
        }

        static async void SqlServerTableDefineToHTML(string prefix)
        {
            var htmlTask = GetHtmlDocOriginAsync();

            #region 查询
            var connectString = appConfiguration.GetConnectionString("mssql");
            var dbcontext = new MsSqlContext(new DefaultDbConnectionFactory(connectString));
            //数据库名
            var dbname = dbcontext.SqlQuery<string>("select db_name()").First();
            //所有表名及描述
            var tables_desc = dbcontext.SqlQuery<KeyValuePair<string, string>>($@"select tbs.name [Key],ds.value [Value] from {dbname}..sysobjects tbs
            left join sys.extended_properties ds on tbs.id=ds.major_id and ds.minor_id=0 where tbs.xtype='U' and tbs.name <> 'sysdiagrams' order by [Key]").ToList();//dbname..可省略
            if (string.IsNullOrEmpty(prefix))
                tables_desc = tables_desc.Where(p => p.Key.StartsWith(prefix)).ToList();

            //所有表设计
            var tables_design = dbcontext.SqlQuery<TableDesign>(@"SELECT obj.name AS 表名,
            col.colorder AS 序号,
            col.name AS 列名,
            ISNULL(ep.[value], '') AS 列说明,
            t.name AS 数据类型,
            col.length AS 长度MS,
            ISNULL(COLUMNPROPERTY(col.id, col.name, 'Scale'), 0) AS 小数,
            CASE WHEN COLUMNPROPERTY(col.id, col.name, 'IsIdentity') = 1 THEN '√' ELSE '' END AS 标识,
            CASE WHEN EXISTS (SELECT 1 
            FROM dbo.sysindexes si 
            INNER JOIN dbo.sysindexkeys sik ON si.id = sik.id 
            AND si.indid = sik.indid 
            INNER JOIN dbo.syscolumns sc ON sc.id = sik.id 
            AND sc.colid = sik.colid 
            INNER JOIN dbo.sysobjects so ON so.name = si.name 
            AND so.xtype = 'PK' 
            WHERE sc.id = col.id 
            AND sc.colid = col.colid ) THEN '√' 
            ELSE '' END AS 主键,
            CASE WHEN col.isnullable = 1 THEN '√' ELSE '' END AS 空值,
            ISNULL(comm.text, '') AS 默认值 
            FROM dbo.syscolumns col 
            LEFT JOIN dbo.systypes t ON col.xtype = t.xusertype 
            INNER JOIN dbo.sysobjects obj ON col.id = obj.id AND obj.xtype = 'U' AND obj.status >= 0 ------(xtype = 'U'非用户表)
            LEFT JOIN dbo.syscomments comm ON col.cdefault = comm.id 
            LEFT JOIN sys.extended_properties ep ON col.id = ep.major_id AND col.colid = ep.minor_id AND ep.name = 'MS_Description' 
            LEFT  JOIN sys.extended_properties epTwo ON obj.id = epTwo.major_id AND epTwo.minor_id = 0 AND epTwo.name = 'MS_Description'
            ORDER BY [表名],[序号]").ToList();
            #endregion

            #region 拼接字符串
            var char13 = "\r\n";
            var top_0 = dbname;
            var left_1 = string.Join(char13, tables_desc.Select(t => $"<tr><td><a href='#{t.Key}'>{t.Key} {t.Value}</a></td></tr>"));
            var right_2 = new StringBuilder();
            foreach (var tb in tables_desc)
            {
                right_2.Append($@"<a name='{tb.Key}'></a>
                <table class='det' cellspacing='1' cellpadding='0'>
                    <thead>
                        <tr>
                            <th colspan='10'>{tb.Key} {tb.Value}</th>
                        </tr>
                        <tr>
                            <th width='30'>序号</th>
                            <th width='140'>列名</th>
                            <th>列说明</th>
                            <th width='100'>数据类型</th>
                            <th width='50'>长度</th>
                            <th width='30'>小数</th>
                            <th width='30'>标识</th>
                            <th width='30'>主键</th>
                            <th width='30'>空值</th>
                            <th width='70'>默认值</th>
                        </tr>                                
                    </thead>
                    <tbody>").Append(char13);
                //单个表设计
                var tb_designs = tables_design.Where(t => t.表名 == tb.Key).ToList();
                foreach (var design in tb_designs)
                {
                    right_2.Append($@"<tr>
                    <td>{design.序号}</td>
                    <td>{design.列名}</td>
                    <td>{design.列说明}</td>
                    <td>{design.数据类型}</td>
                    <td>{design.长度MS}</td>
                    <td>{design.小数}</td>
                    <td>{design.标识}</td>
                    <td>{design.主键}</td>
                    <td>{design.空值}</td>
                    <td>{design.默认值}</td>
                    </tr>").Append(char13);
                }
                right_2.Append("</tbody></table><div class='totop' onclick='javascript:location.href=\"#top\"' href='#top'>TOP</div>");
            }
            #endregion
            
            var html = await htmlTask;
            html = string.Format(html, top_0, left_1, right_2);
            File.WriteAllText(top_0 + ".html", html);
        }

        static async void MysqlTableDefineToHTML(string prefix)
        {
            var htmlTask = GetHtmlDocOriginAsync();

            #region 查询
            var connectString = appConfiguration.GetConnectionString("mysql");
            var dbcontext = new MySqlContext(new tpc.MySqlConnectionFactory(connectString));

            //数据库名
            var dbname = dbcontext.SqlQuery<string>("select database()").First();
            //所有表名及描述
            var tables_desc = dbcontext.SqlQuery<KeyValuePair<string, string>>($@"SELECT TABLE_NAME `Key`
            ,TABLE_COMMENT `Value` FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='{dbname}'").ToList();
            if (!string.IsNullOrEmpty(prefix))
                tables_desc = tables_desc.Where(p => p.Key.StartsWith(prefix)).ToList();
            //所有表设计
            var tables_design = dbcontext.SqlQuery<TableDesign>($@"SELECT
            TABLE_NAME AS '表名',
            COLUMN_NAME AS '列名',
            ORDINAL_POSITION AS '序号',
            COLUMN_DEFAULT AS '默认值',
            case IS_NULLABLE when 'YES' then '√' else '' end AS '空值',
            DATA_TYPE AS '数据类型',
            CHARACTER_MAXIMUM_LENGTH AS '长度MY',
            NUMERIC_PRECISION AS '数值最大位数',
            NUMERIC_SCALE AS '小数',
            case COLUMN_KEY when 'PRI' then '√' else '' end as '主键',
            case EXTRA when 'AUTO_INCREMENT' then '√' else '' end AS '标识',
            COLUMN_COMMENT AS '列说明'
        FROM
            information_schema.`COLUMNS`
        WHERE
            TABLE_SCHEMA = '{dbname}'
        ORDER BY
            TABLE_NAME,
            ORDINAL_POSITION;").ToList();
            #endregion

            #region 拼接字符串
            var char13 = "\r\n";
            var top_0 = dbname;
            var left_1 = string.Join(char13, tables_desc.Select(t => $"<tr><td><a href='#{t.Key}'>{t.Key} {t.Value}</a></td></tr>"));
            var right_2 = new StringBuilder();
            foreach (var tb in tables_desc)
            {
                if (!string.IsNullOrEmpty(prefix) && !tb.Key.StartsWith(prefix))
                {
                    continue;
                }

                right_2.Append($@"<a name='{tb.Key}'></a>
                <table class='det' cellspacing='1' cellpadding='0'>
                    <thead>
                        <tr>
                            <th colspan='10'>{tb.Key} {tb.Value}</th>
                        </tr>
                        <tr>
                            <th width='30'>序号</th>
                            <th width='140'>列名</th>
                            <th>列说明</th>
                            <th width='100'>数据类型</th>
                            <th width='50'>长度或整数位数</th>
                            <th width='30'>小数</th>
                            <th width='30'>标识</th>
                            <th width='30'>主键</th>
                            <th width='30'>空值</th>
                            <th width='70'>默认值</th>
                        </tr>                                
                    </thead>
                    <tbody>").Append(char13);
                //单个表设计
                var tb_designs = tables_design.Where(t => t.表名 == tb.Key).ToList();
                foreach (var design in tb_designs)
                {
                    right_2.Append($@"<tr>
                    <td>{design.序号}</td>
                    <td>{design.列名}</td>
                    <td>{design.列说明}</td>
                    <td>{design.数据类型}</td>
                    <td>{(design.长度MY == null ? design.数值最大位数 : design.长度MY)}</td>
                    <td>{design.小数}</td>
                    <td>{design.标识}</td>
                    <td>{design.主键}</td>
                    <td>{design.空值}</td>
                    <td>{design.默认值}</td>
                    </tr>").Append(char13);
                }
                right_2.Append("</tbody></table><div class='totop' onclick='javascript:location.href=\"#top\"' href='#top'>TOP</div>");
            }
            #endregion

            var html = await htmlTask;
            html = string.Format(html, top_0, left_1, right_2);
            File.WriteAllText(top_0 + ".html", html);
        }

        static async Task<string> GetHtmlDocOriginAsync()
        {
            var path_index = System.IO.Directory.GetCurrentDirectory();
            return await File.ReadAllTextAsync(Path.Combine(path_index, "template.html"));
        }
    }
}
