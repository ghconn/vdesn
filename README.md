# vdesn
数据库表结构导出工具

导出为单个html文件

使用方法

1，安装dotnet core 2.2 sdk

![演示](https://github.com/ghconn/vdesn/blob/master/1.png)

2，修改dist目录appsettings.json内的连接字符串，支持sql server和mysql数据库，需要哪种改相应的一种即可。

3，cd命令到dist目录，使用命令dotnet desn.dll mysql或者dotnet desn.dll mssql即可。

4，dist目录将生成 数据库名.html 文件。
