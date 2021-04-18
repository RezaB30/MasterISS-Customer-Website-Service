using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class CustomerAuthenticationWithPasswordResponse
    {
        [DataMember]
        public string ValidDisplayName { get; set; }
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public string SubscriberNo { get; set; }
        [DataMember]
        public string[] RelatedCustomers { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceCustomerAuthenticationWithPasswordResponse : BaseResponse<CustomerAuthenticationWithPasswordResponse, SHA1>
    {
        public CustomerServiceCustomerAuthenticationWithPasswordResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public CustomerAuthenticationWithPasswordResponse AuthenticationWithPasswordResult { get { return Data; } set { Data = value; } }
    }
}