using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class ResponseValidationBase
    {
        [DataMember]
        public IEnumerable<ValidationElement> ValidationElements { get; set; }
    }
    [DataContract]
    public class ValidationElement
    {
        [DataMember]
        public string Key { get; set; }
        [DataMember]
        public string Value { get; set; }
    }
    public static class Validator
    {
        public static ValidationElement Required(string value , string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new ValidationElement()
                {
                    Key = "Required",
                    Value = string.Format(RadiusR.API.CustomerWebApi.Localization.ErrorMessages.Required, paramName)
                };
            }
            return null;
        }
        public static ValidationElement MaxLength(string value, int length , string paramName)
        {
            if (value.Length > length)
            {
                return new ValidationElement()
                {
                    Key = "MaxLength",
                    Value = string.Format(RadiusR.API.CustomerWebApi.Localization.ErrorMessages.MaxLength, paramName, length)
                };
            }
            return null;
        }
        public static ValidationElement ValidateEmail(string value , string paramName)
        {
            Regex regex = new Regex(@"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z");
            var validate = regex.Match(value);
            if (!validate.Success)
            {
                return new ValidationElement()
                {
                    Key = "InvalidEmail",
                    Value = string.Format(RadiusR.API.CustomerWebApi.Localization.ErrorMessages.InvalidEmail, paramName)
                };
            }
            return null;
        }
        //@"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z"
    }
}