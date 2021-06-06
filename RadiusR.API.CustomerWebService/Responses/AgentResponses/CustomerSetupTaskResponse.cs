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
    public class CustomerSetupTaskResponse
    {
        [DataMember]
        public long ID { get; set; }        
        [DataMember]
        public NameValuePair SetupUser { get; set; }
        [DataMember]
        public long SubscriptionID { get; set; }
        [DataMember]
        public string ValidDisplayName { get; set; }
        [DataMember]
        public NameValuePair TaskType { get; set; }
        [DataMember]
        public bool HasModem { get; set; }
        [DataMember]
        public string ModemName { get; set; }
        [DataMember]
        public string TaskIssueDate { get; set; }
        [DataMember]
        public string CompletionDate { get; set; }
        [DataMember]
        public NameValuePair TaskStatus { get; set; }
        [DataMember]
        public string Details { get; set; }
        [DataMember]
        public NameValuePair AllowanceState { get; set; }
        [DataMember]
        public decimal? Allowance { get; set; }
        [DataMember]
        public TaskUpdates[] CustomerTaskUpdates { get; set; }
        [DataContract]
        public class TaskUpdates
        {
            [DataMember]
            public long ID { get; set; }
            [DataMember]
            public NameValuePair FaultCode { get; set; }
            [DataMember]
            public string Description { get; set; }
            [DataMember]
            public string Date { get; set; }
            [DataMember]
            public string ReservationDate { get; set; }
        }
    }
    [DataContract]
    public partial class AgentServiceCustomerSetupTaskResponse : BaseResponse<CustomerSetupTaskResponse[], SHA256>
    {
        public AgentServiceCustomerSetupTaskResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public CustomerSetupTaskResponse[] CustomerTaskList { get { return Data; } set { Data = value; } }
    }
}