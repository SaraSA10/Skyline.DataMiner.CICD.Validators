using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skyline.DataMiner.CICD.Validators.Common.Interfaces;

namespace ValidatorRunnerMajorChangeChecker
{
    public class FilteredResults
    {
        public IList<IValidationResult> Errors { get; set; }
        public IList<IValidationResult> SuppressedErrors { get; set; }
        //public IList<IValidationResult> InfoMessages { get; set; }

        public FilteredResults()
        {
            Errors = new List<IValidationResult>();
            SuppressedErrors = new List<IValidationResult>();
            //InfoMessages = new List<IValidationResult>();
        }
    }
}

