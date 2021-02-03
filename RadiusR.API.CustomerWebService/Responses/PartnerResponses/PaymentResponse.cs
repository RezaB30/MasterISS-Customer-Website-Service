using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public partial class PartnerServicePaymentResponse : BaseResponse<long[], SHA1>
    {
        public PartnerServicePaymentResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public long[] PaymentResponse { get { return Data; } set { Data = value; } }
    }
}