using SqlReportFileAction;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;

namespace WishBatchSettingShipFee
{
    public class SetShipFeeAction : IReportRowAction
    {
        public bool MustSupportConcurrency { set; get; }

        Func<string, string> GetCurrentResult()
        {
            if (MustSupportConcurrency)
            {
                return r =>
                {
                    using (HttpClient c = new HttpClient())
                    {
                        return c.DownloadString(r);
                    }
                };
            }
            else
            {
                return HttpClient.Instance.DownloadString;
            }
        }

        static string TageInnerText(string xml, string tagName)
        {
            string tagStart = "<" + tagName;
            int idx = xml.IndexOf(tagStart);
            if (idx == -1)
            {
                return null;
            }
            else
            {
                int idxTagEnd = xml.IndexOf(">", idx + tagStart.Length);
                if (idxTagEnd == -1 || xml[idxTagEnd - 1] == '/')
                {
                    //"<Tag />"
                    return string.Empty;
                }
                else
                {
                    int idxEndTag = xml.IndexOf("</" + tagName + ">", idxTagEnd);
                    if (idxEndTag > idxTagEnd)
                    {
                        return xml.Substring(idxTagEnd + 1, idxEndTag - 1 - idxTagEnd);
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }
        }

        int retryTimes = 0; //重试次数，成功后至0
        LimitQueue<string> errorCodeQue = new LimitQueue<string>(5);
        string lastAccessToken = string.Empty;

        public void Execute(ReportRow row, Action<string, object[]> log = null)
        {
            if (row["AccessToken"] == lastAccessToken && errorCodeQue.IsFull() && errorCodeQue.Distinct().Count() == 1)
            {
                //重复5次以上错误，略过处理
                return;
            }

            /*
            <?xml version="1.0" encoding="utf-8"?>
            <Response>
            <Code>2000</Code>
            <Message>Merchant unable to access API due to account's status.</Message>
            <Data></Data>
            </Response>
             */

            string apiFormat = "https://china-merchant.wish.com/api/v2/product/update-multi-shipping";
            string apiRequestUrl = string.Format("access_token={0}&format=xml&id={1}", row["AccessToken"], row["WishId"]);
            string responseText = string.Empty;
            string responseCode = "0", errorFormat = "#WishId:{0}, Error:{1}";

            string appendParam = AppendProductShipFeeParameters(Convert.ToDouble(row["SoldPrice"]), Convert.ToDouble(row["ShippingFee"]));
            if (appendParam.Length > 3)
            {
                apiRequestUrl += appendParam;
            }
            else
            {
                return;
            }

         InvokeStart:
            try
            {
                //responseText = Encoding.UTF8.GetString(NC.UploadData(apiFormat, Encoding.UTF8.GetBytes(apiRequestUrl)));
                responseText = GetCurrentResult()(apiFormat + "?" + apiRequestUrl);
                responseCode = TageInnerText(responseText, "Code");
                if (responseCode != "0")
                {
                    if (log != null)
                    {
                        log(errorFormat, new object[] { row["WishId"], responseText });
                    }
                    else
                    {
                        Console.WriteLine(string.Format(errorFormat, row["WishId"], responseText));
                    }
                }
                else
                {
                    retryTimes = 0;
                }
            }
            catch (WebException wEx)
            {
                if (wEx.Response != null)
                {
                    using (StreamReader sr = new StreamReader(wEx.Response.GetResponseStream()))
                    {
                        responseText = sr.ReadToEnd();
                        sr.Close();
                    }
                }

                responseText = string.IsNullOrEmpty(responseText) ? wEx.Message : responseText;
                responseCode = TageInnerText(responseText, "Code");
                if (string.IsNullOrEmpty(responseCode))
                {
                    //没有错误的Code
                    retryTimes++;
                    if (retryTimes < 3)
                    {
                        System.Threading.Thread.Sleep(1000 * 3);
                        goto InvokeStart;
                    }
                }
                else
                {
                    errorCodeQue.Enqueue(responseCode);
                    string errMsg = TageInnerText(responseText, "Message");
                    if (log != null)
                    {
                        log("URL:{0}, Error:{1}", new object[] { apiRequestUrl, errMsg });
                    }
                    else
                    {
                        Console.WriteLine(string.Format("URL:{0}, Error:{1}", apiRequestUrl, errMsg));
                    }
                }
            }
            catch (Exception ncEx)
            {
                if (log != null)
                {
                    log("URL:{0}, Error:{1}", new object[] { apiRequestUrl, ncEx.Message });
                }

                retryTimes++;
                if (retryTimes < 3)
                {
                    System.Threading.Thread.Sleep(1000 * 3);
                    goto InvokeStart;
                }
            }
            finally
            {
                lastAccessToken = row["AccessToken"];
            }
        }

        static string AppendProductShipFeeParameters(double productFee, double shipFee)
        {
            //http://192.168.1.228/issues/12840
            StringBuilder pb = new StringBuilder();
            double totalFee = productFee + shipFee;
            if (totalFee >= 10.0d)
            {
                //墨西哥(MX)、俄罗斯(RU)、意大利(IT)、西班牙(ES)、丹麦(DK)、瑞典(SE)在产品运费的基础上增加3美金
                //巴西(BR)、哥斯达黎加(CR)在产品运费的基础上增加5美金
                pb.AppendFormat("&MX={0}&RU={0}&IT={0}&ES={0}&DK={0}&SE={0}", shipFee + 3);
                pb.AppendFormat("&BR={0}&CR={0}", shipFee + 5);
            }
            else if (totalFee < 10 && totalFee >= 7)
            {
                //墨西哥(MX)、俄罗斯(RU)、意大利(IT)在产品运费的基础上增加3美金
                //巴西(BR)在产品运费的基础上增加5美金
                pb.AppendFormat("&MX={0}&RU={0}&IT={0}", shipFee + 3);
                pb.AppendFormat("&BR={0}", shipFee + 5);
            }
            else if (totalFee < 7 && totalFee >= 3)
            {
                //墨西哥(MX)、俄罗斯(RU)在产品运费的基础上增加3美金
                pb.AppendFormat("&MX={0}&RU={0}", shipFee + 3);
            }
            return pb.ToString();
        }

        public string[] MustContaineFields
        {
            get
            {
                return "AccessToken WishId SoldPrice ShippingFee".Split(' ');
            }
        }
    }
}
