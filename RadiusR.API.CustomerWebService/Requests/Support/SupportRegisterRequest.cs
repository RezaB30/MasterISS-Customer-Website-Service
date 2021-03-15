using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for SupportRegisterRequest
/// </summary>
/// 
namespace RadiusR.API.CustomerWebService.Requests.Support
{
    [DataContract]
    public class SupportRegisterRequest : BaseSubscriptionRequest
    {
        [DataMember]
        public int? RequestTypeId { get; set; }
        [DataMember]
        public int? SubRequestTypeId { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public List<Attachment> Attachments { get; set; }
    }
    [DataContract]
    public class Attachment
    {
        [DataMember]
        public byte[] FileContent { get; set; }
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public string FileExtention { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceSupportRegisterRequest : BaseRequest<SupportRegisterRequest, SHA1>
    {
        [DataMember]
        public SupportRegisterRequest SupportRegisterParameters
        {
            get
            {
                return Data;
            }
            set
            {
                Data = value;
            }
        }
    }
}
