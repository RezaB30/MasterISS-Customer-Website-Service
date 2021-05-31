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
    public partial class CustomerServiceBillPayableAmountResponse : BaseResponse<decimal?, SHA1>
    {
        public CustomerServiceBillPayableAmountResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public decimal? PayableAmount { get { return Data; } set { Data = value; } }
    }
}