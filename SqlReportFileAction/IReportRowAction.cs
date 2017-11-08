
using System;

namespace SqlReportFileAction
{
    public interface IReportRowAction
    {
        /// <summary>
        /// 设置要求是否支持并行操作
        /// </summary>
        bool MustSupportConcurrency { set; get; }

        void Execute(ReportRow row, Action<string, object[]> log = null);

        /// <summary>
        /// 必须包含的字段列表
        /// </summary>
        string[] MustContaineFields { get; }

    }
}
