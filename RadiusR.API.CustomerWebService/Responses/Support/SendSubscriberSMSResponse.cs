using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public partial class CustomerServiceSendSubscriberSMSResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceSendSubscriberSMSResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? SendSubscriberSMSResponse { get { return Data; } set { Data = value; } }
    }
}