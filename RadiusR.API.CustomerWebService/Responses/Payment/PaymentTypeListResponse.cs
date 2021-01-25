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
    public partial class CustomerServicePaymentTypeListResponse : BaseResponse<Dictionary<int,string>, SHA1>
    {
        public CustomerServicePaymentTypeListResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public Dictionary<int, string> PaymentTypes { get { return Data; } set { Data = value; } }
    }
}