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
    public class GetCustomerSpecialOffersResponse
    {
        [DataMember]
        public string ReferenceNo { get; set; }
        [DataMember]
        public DateTime StartDate { get; set; }
        [DataMember]
        public DateTime EndDate { get; set; }
        [DataMember]
        public int UsedCount { get; set; }
        [DataMember]
        public int TotalCount { get; set; }
        [DataMember]
        public int MissedCount { get; set; }
        [DataMember]
        public int RemainingCount { get; set; }
        [DataMember]
        public bool IsApplicableThisPeriod { get; set; }
        [DataMember]
        public bool IsCancelled { get; set; }
        [DataMember]
        public short? ReferralSubscriberState { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetCustomerSpecialOffersResponse : BaseResponse<IEnumerable<GetCustomerSpecialOffersResponse>, SHA1>
    {
        public CustomerServiceGetCustomerSpecialOffersResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public IEnumerable<GetCustomerSpecialOffersResponse> GetCustomerSpecialOffersResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
