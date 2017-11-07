using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SqlReportFileAction
{
    class Program
    {
        static readonly Type[] AllRowActionTypes = FindRowActionType(typeof(IReportRowAction));

        static void Main(string[] args)
        {
            //System.Diagnostics.Debugger.Launch();
            if (args != null && args.Length > 0)
            {
                if (args.Length == 3 && args[1].StartsWith("--"))
                {
                    string groupKey = args[2];
                    if (args[1] == "--split")
                    {
                        #region 拆分
                        using (SqlReportFile file = new SqlReportFile(args[0]))
                        {
                            if (file.Columns.Exists(c => c.ColumnName == groupKey))
                            {
                                file.QueryMatchItems().GroupBy(r => r[groupKey]).ToList().ForEach(g =>
                                {
                                    file.BuildSplitFileWithRows(g.ToList(), g.Key + ".rpt");
                                });
                            }
                            else
                            {
                                Console.WriteLine("检测到需要拆分文件，但字段" + groupKey + "在文件中不存在！");
                            }
                        }
                        #endregion
                    }
                    else if (args[1] == "--exec")
                    {
                        #region 执行
                        using (SqlReportFile file = new SqlReportFile(args[0]))
                        {
                            if (file.Columns.Exists(c => c.ColumnName == groupKey))
                            {
                                List<IReportRowAction> actions = getAllActions();
                                actions = actions.Where(t => t.MustContaineFields.All(f => file.Columns.Exists(c => c.ColumnName == f))).ToList();
                                if (actions.Any() == false)
                                {
                                    Console.WriteLine("当前文件不是目前处理数据报表的组件操作类的数据！");
                                }
                                else
                                {
                                    #region 并行分组执行
                                    actions.ForEach(r => r.MustSupportConcurrency = true);
                                    List<StreamWriter> logs = new List<StreamWriter>();
                                    try
                                    {
                                        file.QueryMatchItems().GroupBy(r => r[groupKey]).AsParallel()
                                        .WithExecutionMode(ParallelExecutionMode.Default)
                                        .ForAll(ga =>
                                        {
                                            FileStream fs = new FileStream(ga.Key + ".log", FileMode.Append, FileAccess.Write, FileShare.Read, 8);
                                            StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Default);
                                            sw.AutoFlush = true;
                                            logs.Add(sw);
                                            ExecReportRowActions(actions, ga, sw.WriteLine);
                                        });
                                    }
                                    catch (AggregateException aEx)
                                    {
                                        if (!System.Diagnostics.Debugger.IsAttached)
                                            System.Diagnostics.Debugger.Launch();
                                        Console.WriteLine(aEx.Message);
                                    }
                                    finally
                                    {
                                        logs.ForEach(l =>
                                        {
                                            l.Close();
                                            l.Dispose();
                                        });
                                    }
                                    #endregion
                                }
                            }
                            else
                            {
                                Console.WriteLine("检测到需要拆分文件，但字段" + groupKey + "在文件中不存在！");
                            }
                        }
                        #endregion
                    }
                }
                else
                {
                    if (AllRowActionTypes == null || AllRowActionTypes.Length == 0)
                    {
                        Console.WriteLine("没有找到处理数据报表的组件操作类！");
                    }
                    else
                    {
                        #region 执行行级数据处理
                        List<IReportRowAction> actions = getAllActions();
                        using (SqlReportFile file = new SqlReportFile(args[0]))
                        {
                            actions = actions.Where(t => t.MustContaineFields.All(f => file.Columns.Exists(c => c.ColumnName == f))).ToList();
                            if (actions.Any() == false)
                            {
                                Console.WriteLine("当前文件不是目前处理数据报表的组件操作类的数据！");
                            }
                            else
                            {
                                file.FindRowAction(r =>
                                {
                                    actions.ForEach(t =>
                                    {
                                        try
                                        {
                                            t.Execute(r);
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                        }
                                    });
                                }, true);
                            }
                        }
                        #endregion
                    }
                }
            }
            Console.WriteLine("处理完成！");
            Console.Read();

        }

        static List<IReportRowAction> getAllActions()
        {
            List<IReportRowAction> actions = new List<IReportRowAction>();
            foreach (Type t in AllRowActionTypes)
            {
                IReportRowAction rowAct = Activator.CreateInstance(t) as IReportRowAction;
                actions.Add(rowAct);
            }
            return actions;
        }

        static void ExecReportRowActions(List<IReportRowAction> actions, IEnumerable<ReportRow> allRows, Action<string, object[]> log = null)
        {
            var rowList = allRows.ToList();
            for (int i = 0, j = rowList.Count(); i < j; i++)
            {
                var row = rowList[i];

                actions.ForEach(t =>
                {
                    try
                    {
                        t.Execute(row);
                    }
                    catch (Exception rowEx)
                    {
                        if (log != null)
                        {
                            log("##{0},行数据为：{1}", new object[] { rowEx.Message, row.ToString() });
                        }
                        else
                        {
                            Console.WriteLine(rowEx.Message);
                        }
                    }
                });

                //string progress = string.Format("Progress:{0}", ((double)i / (double)j).ToString("P2"));
                //Console.WriteLine(progress);
            }
        }

        static Type[] FindRowActionType(Type interfaceType)
        {
            List<Type> typeList = new List<Type>();
            foreach (var dllFile in new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).GetFiles("*.dll", SearchOption.AllDirectories))
            {
                Assembly oneAsmFile = null;
                try
                {
                    oneAsmFile = Assembly.LoadFrom(dllFile.FullName);
                }
                catch (Exception) { }

                if (oneAsmFile != null)
                {
                    Type[] allTypeLoaded = null;
                    try
                    {
                        allTypeLoaded = oneAsmFile.GetTypes();
                    }
                    catch (Exception) { }

                    #region 加载识别类型
                    if (allTypeLoaded != null && allTypeLoaded.Length > 0)
                    {
                        foreach (Type t in allTypeLoaded)
                        {
                            if (t.IsClass && t.GetInterfaces().Any(m => m == interfaceType))
                            {
                                typeList.Add(t);
                            }
                        }
                    }
                    #endregion

                }
            }
            return typeList.ToArray();
        }

    }
}
