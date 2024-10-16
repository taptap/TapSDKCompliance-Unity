using System.Collections.Generic;
using TapSDK.Core;
using UnityEngine;
using Newtonsoft.Json;

namespace TapSDK.Compliance
{
    public class TapTapComplianceOption: TapTapSdkBaseOptions
    {
        public static TapTapComplianceOption Config { get; set; }

        [JsonProperty("moduleName")]
        private string _moduleName = "TapTapCompliance";
        [JsonIgnore]
        public string moduleName
        {
            get => _moduleName;
        }

        public  AndroidJavaObject androidObject => 
             new AndroidJavaObject("com.taptap.sdk.compilance.option.TapTapComplianceOptions", showSwitchAccount, useAgeRange);
        

        internal string clientId;
        
        
        public bool showSwitchAccount = false;


        public bool useAgeRange = true;

        
        public TapTapComplianceOption(bool useAgeRange, bool showSwitchAccount){
            this.useAgeRange = useAgeRange;
            this.showSwitchAccount = showSwitchAccount;
        }

        public TapTapComplianceOption(){
            showSwitchAccount = false;
            useAgeRange = true;
        }
        
        
        public Dictionary<string, object> ToDict() {
            return new Dictionary<string, object> {
                ["showSwitchAccount"] = showSwitchAccount,
                ["useAgeRange"] = useAgeRange
            };
        }
    }
}

