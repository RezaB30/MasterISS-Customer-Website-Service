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
    public class PartnerServiceIDCardValidationResponse : BaseResponse<bool, SHA256>
    {
        public PartnerServiceIDCardValidationResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool IDCardValidationResponse { get { return Data; } set { Data = value; } }
    }
}