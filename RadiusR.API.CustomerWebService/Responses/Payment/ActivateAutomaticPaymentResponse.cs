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
    public partial class CustomerServiceActivateAutomaticPaymentResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceActivateAutomaticPaymentResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? IsSuccess { get { return Data; } set { Data = value; } }
    }
}