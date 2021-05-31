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
    public class ServiceAvailabilityResponse
    {
        [DataMember]
        public ADSLInfo ADSL { get; set; }
        [DataMember]
        public VDSLInfo VDSL { get; set; }
        [DataMember]
        public FIBERInfo FIBER { get; set; }
        [DataContract]
        public class ADSLInfo
        {
            [DataMember]
            public bool HasInfrastructureAdsl { get; set; }
            [DataMember]
            public int? AdslSpeed { get; set; }
            [DataMember]
            public int? AdslDistance { get; set; }
            [DataMember]
            public string AdslPortState { get; set; }
            [DataMember]
            public string AdslSVUID { get; set; }
        }
        public class VDSLInfo
        {
            [DataMember]
            public bool HasInfrastructureVdsl { get; set; }
            [DataMember]
            public int? VdslSpeed { get; set; }
            [DataMember]
            public int? VdslDistance { get; set; }
            [DataMember]
            public string VdslPortState { get; set; }
            [DataMember]
            public string VdslSVUID { get; set; }
        }
        public class FIBERInfo
        {
            [DataMember]
            public bool HasInfrastructureFiber { get; set; }
            [DataMember]
            public int? FiberSpeed { get; set; }
            [DataMember]
            public int? FiberDistance { get; set; }
            [DataMember]
            public string FiberPortState { get; set; }
            [DataMember]
            public string FiberSVUID { get; set; }
        }
        [DataMember]
        public string address { get; set; }
        [DataMember]
        public string BBK { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceServiceAvailabilityResponse : BaseResponse<ServiceAvailabilityResponse, SHA1>
    {
        public CustomerServiceServiceAvailabilityResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public ServiceAvailabilityResponse ServiceAvailabilityResponse
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