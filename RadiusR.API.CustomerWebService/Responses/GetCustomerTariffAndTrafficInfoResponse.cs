using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class GetCustomerTariffAndTrafficInfoResponse
    {
        [DataMember]
        public string ServiceName { get; set; }
        //[DataMember]
        //public int BillCount { get; set; }
        //[DataMember]
        //public string BillsTotal { get; set; }
        [DataMember]
        public long Download { get; set; }
        [DataMember]
        public long Upload { get; set; }
        [DataMember]
        public IEnumerable<GetCustomerUsageInfoResponse> MonthlyUsage { get; set; }
        [DataMember]
        public long? BaseQuota { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetCustomerTariffAndTrafficInfoResponse : BaseResponse<GetCustomerTariffAndTrafficInfoResponse, SHA1>
    {
        public CustomerServiceGetCustomerTariffAndTrafficInfoResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetCustomerTariffAndTrafficInfoResponse GetCustomerTariffAndTrafficInfoResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}

