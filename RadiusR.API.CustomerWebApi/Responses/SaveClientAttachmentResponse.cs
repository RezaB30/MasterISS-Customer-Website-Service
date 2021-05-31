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
    public partial class CustomerServiceSaveClientAttachmentResponse : BaseResponse<bool?, SHA1>
    {
        public CustomerServiceSaveClientAttachmentResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public bool? SaveClientAttachmentResult { get { return Data; } set { Data = value; } }
    }
}