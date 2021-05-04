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
    public partial class PartnerServiceSaveClientAttachmentResponse : BaseResponse<bool?, SHA256>
    {
        public PartnerServiceSaveClientAttachmentResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? SaveClientAttachmentResult { get { return Data; } set { Data = value; } }
    }
}