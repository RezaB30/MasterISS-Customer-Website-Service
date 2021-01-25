using RadiusR.API.CustomerWebService.Enums;
using RadiusR.DB;
using RadiusR.DB.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RadiusR.API.CustomerWebService
{
    public static class SupportUtilities
    {
        public static Tuple<string, int> SupportRequestDisplayState(long _subscriptionId, long _supportRequestId)
        {
            using (var db = new RadiusR.DB.RadiusREntities())
            {
                var SupportRequest = db.SupportRequests.Find(_supportRequestId);
                if (SupportRequest.CustomerApprovalDate == null)
                {
                    if (SupportRequest.StateID == (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done)
                    {
                        var PassedTimeSpan = CustomerWebsiteSettings.SupportRequestPassedTime;
                        var IsPassedTime = (DateTime.Now - SupportRequest.SupportRequestProgresses.OrderByDescending(s => s.Date).Select(s => s.Date).FirstOrDefault()) < PassedTimeSpan ? false : true;
                        if (IsPassedTime)
                        {
                            return new Tuple<string, int>(SupportRequestDisplayTypes.NoneDisplay.ToString(), (int)SupportRequestDisplayTypes.NoneDisplay);
                        }
                        else
                        {
                            if (HasOpenRequest(_subscriptionId))
                            {
                                return new Tuple<string, int>(SupportRequestDisplayTypes.NoneDisplay.ToString(), (int)SupportRequestDisplayTypes.NoneDisplay);
                            }
                            else
                            {
                                return new Tuple<string, int>(SupportRequestDisplayTypes.OpenRequestAgainDisplay.ToString(), (int)SupportRequestDisplayTypes.OpenRequestAgainDisplay);
                            }
                        }
                    }
                    else
                    {
                        return new Tuple<string, int>(SupportRequestDisplayTypes.AddNoteDisplay.ToString(), (int)SupportRequestDisplayTypes.AddNoteDisplay);
                    }
                }
                else
                {
                    return new Tuple<string, int>(SupportRequestDisplayTypes.NoneDisplay.ToString(), (int)SupportRequestDisplayTypes.NoneDisplay);
                }
            }

        }
        public static bool HasOpenRequest(long _subscriptionId)
        {
            using (var db = new RadiusR.DB.RadiusREntities())
            {
                var HasOpenRequest = db.SupportRequests
                    .Where(m => m.IsVisibleToCustomer && m.StateID == (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.InProgress && _subscriptionId == m.SubscriptionID)
                    //.FirstOrDefault() != null;
                    .Any();
                return HasOpenRequest;
            }
        }
        public static SupportRequestAvailableTypes SupportRequestAvailable(long subscriptionId, long supportRequestId)
        {
            using (var db = new RadiusR.DB.RadiusREntities())
            {
                var SupportRequest = db.SupportRequests.Find(supportRequestId);
                if (SupportRequest.CustomerApprovalDate == null)
                {
                    if (SupportRequest.StateID == (short)RadiusR.DB.Enums.SupportRequests.SupportRequestStateID.Done)
                    {
                        var PassedTimeSpan = CustomerWebsiteSettings.SupportRequestPassedTime;
                        var IsPassedTime = (DateTime.Now - SupportRequest.SupportRequestProgresses.OrderByDescending(s => s.Date).Select(s => s.Date).FirstOrDefault()) < PassedTimeSpan ? false : true;
                        if (IsPassedTime)
                        {
                            return SupportRequestAvailableTypes.None;
                        }
                        else
                        {
                            if (HasOpenRequest(subscriptionId))
                            {
                                return SupportRequestAvailableTypes.None;
                            }
                            else
                            {
                                return SupportRequestAvailableTypes.OpenRequestAgain;
                            }
                        }
                    }
                    else
                    {
                        return SupportRequestAvailableTypes.AddNote;
                    }
                }
                else
                {
                    return SupportRequestAvailableTypes.None;
                }
            }
        }
    }
    public enum SupportRequestDisplayTypes
    {
        NoneDisplay = 1,
        OpenRequestAgainDisplay = 2,
        AddNoteDisplay = 3
    }
}