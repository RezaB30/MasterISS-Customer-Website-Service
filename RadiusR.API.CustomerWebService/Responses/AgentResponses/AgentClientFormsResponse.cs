using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.AgentResponses
{
    [DataContract]
    public class AgentClientFormsResponse
    {
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string MIMEType { get; set; }
        [DataMember]
        public int FormType { get; set; }
    }
    [DataContract]
    public partial class AgentServiceClientFormsResponse : BaseResponse<AgentClientFormsResponse, SHA256>
    {
        public AgentServiceClientFormsResponse(string passwordHash, BaseRequest<SHA256> request) : base(passwordHash, request) { }
        [DataMember]
        public AgentClientFormsResponse AgentClientForms { get { return Data; } set { Data = value; } }
    }
}