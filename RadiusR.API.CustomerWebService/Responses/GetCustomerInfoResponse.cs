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
    public class GetCustomerInfoResponse
    {
        [DataMember]
        public bool HashAutoPayment { get; set; }
        [DataMember]
        public string StaticIP { get; set; }
        [DataMember]
        public string OnlinePassword { get; set; }
        [DataMember]
        public short CustomerState { get; set; }
        [DataMember]
        public string CustomerStateText { get; set; }
        [DataMember]
        public string ValidDisplayName { get; set; }
        [DataMember]
        public string PhoneNo { get; set; }
        [DataMember]
        public string EMail { get; set; }
        [DataMember]
        public string InstallationAddress { get; set; }
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public string Password { get; set; }
        [DataMember]
        public string TTSubscriberNo { get; set; }
        [DataMember]
        public string ReferenceNo { get; set; }
        [DataMember]
        public string PSTN { get; set; }
        [DataMember]
        public string CurrentSubscriberNo { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceGetCustomerInfoResponse : BaseResponse<GetCustomerInfoResponse, SHA1>
    {
        public CustomerServiceGetCustomerInfoResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public GetCustomerInfoResponse GetCustomerInfoResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
