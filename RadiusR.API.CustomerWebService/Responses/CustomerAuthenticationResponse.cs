using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for CustomerAuthenticationResponse
/// </summary>
/// 

namespace RadiusR.API.CustomerWebService.Responses
{
    [DataContract]
    public class CustomerAuthenticationResponse
    {
        [DataMember]
        public long SubscriptionCount { get; set; }
        [DataMember]
        public long CurrentSubscriptionId { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceCustomerAuthenticationResponse : BaseResponse<CustomerAuthenticationResponse, SHA1>
    {
        public CustomerServiceCustomerAuthenticationResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public CustomerAuthenticationResponse CustomerAuthenticationResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
