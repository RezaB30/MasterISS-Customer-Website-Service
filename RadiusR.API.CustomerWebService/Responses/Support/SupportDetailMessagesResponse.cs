using RadiusR.DB.Settings;
using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

/// <summary>
/// Summary description for SupportDetailMessagesResponse
/// </summary>

namespace RadiusR.API.CustomerWebService.Responses.Support
{
    [DataContract]
    public class SupportDetailMessagesResponse
    {
        [DataMember]
        public SupportRequestDisplay SupportRequestDisplayType { get; set; }

        [DataMember]
        public DateTime? CustomerApprovalDate { get; set; }
        [DataMember]
        public DateTime SupportDate { get; set; }
        [DataMember]
        public string SupportNo { get; set; }
        [DataMember]
        public string SupportRequestName { get; set; }
        [DataMember]
        public string SupportRequestSubName { get; set; }
        [DataMember]
        public long ID { get; set; }
        [DataMember]
        public StateType State { get; set; }
        [DataMember]
        public IEnumerable<SupportMessageList> SupportMessages { get; set; }

        [DataContract]
        public class SupportRequestDisplay
        {
            [DataMember]
            public string SupportRequestDisplayTypeName { get; set; }
            [DataMember]
            public int SupportRequestDisplayTypeId { get; set; }
        }
        [DataContract]
        public class SupportMessageList
        {
            [DataMember]
            public string Message { get; set; }
            [DataMember]
            public DateTime MessageDate { get; set; }
            [DataMember]
            public bool IsCustomer { get; set; }
            [DataMember]
            public long StageId { get; set; }
        }
        [DataContract]
        public class StateType
        {
            [DataMember]
            public string StateName { get; set; }
            [DataMember]
            public int StateId { get; set; }
        }
    }
    [DataContract]
    public partial class CustomerServiceSupportDetailMessagesResponse : BaseResponse<SupportDetailMessagesResponse, SHA1>
    {
        public CustomerServiceSupportDetailMessagesResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public SupportDetailMessagesResponse SupportDetailMessagesResponse
        {
            get { return Data; }
            set { Data = value; }
        }
    }
}
