using SqlReportFileAction;
using System;
using System.IO;
using System.Net;
using System.Text;

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

        public void Execute(ReportRow row)
        {
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
            try
            {
                //string aprResult = NC.DownloadString(apiRequestUrl);
                string appendParam = AppendProductShipFeeParameters(Convert.ToDouble(row["SoldPrice"]), Convert.ToDouble(row["ShippingFee"]));
                if (appendParam.Length > 3)
                {
                    apiRequestUrl += appendParam;
                }
                else
                {
                    return;
                }

                //responseText = Encoding.UTF8.GetString(NC.UploadData(apiFormat, Encoding.UTF8.GetBytes(apiRequestUrl)));
                responseText = GetCurrentResult()(apiFormat + "?" + apiRequestUrl);
                if (responseText.IndexOf("<Code>0</Code>") == -1)
                {
                    Console.WriteLine(row["WishId"]);
                    Console.WriteLine(responseText);
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
                else
                {
                    Console.WriteLine(wEx.Message);
                }
            }
            catch (Exception ncEx)
            {
                Console.WriteLine(apiRequestUrl);
                Console.WriteLine(ncEx.Message);
                System.Threading.Thread.Sleep(1000 * 60 * 1);
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
