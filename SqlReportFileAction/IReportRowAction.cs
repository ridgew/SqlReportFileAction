
namespace SqlReportFileAction
{
    public interface IReportRowAction
    {
        /// <summary>
        /// 是否支持并行操作
        /// </summary>
        bool MustSupportConcurrency { set; get; }

        void Execute(ReportRow row);

        /// <summary>
        /// 必须包含的字段列表
        /// </summary>
        string[] MustContaineFields { get; }

    }
}
