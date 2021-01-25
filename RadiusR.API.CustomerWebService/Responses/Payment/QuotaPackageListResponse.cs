using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Payment
{
    [DataContract]
    public class QuotaPackageListResponse
    {
        [DataMember]
        public int ID { get; set; }
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public long Amount { get; set; }
        [DataMember]
        public decimal Price { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceQuotaPackagesResponse : BaseResponse<IEnumerable<QuotaPackageListResponse>, SHA1>
    {
        public CustomerServiceQuotaPackagesResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public IEnumerable<QuotaPackageListResponse> QuotaPackageListResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}