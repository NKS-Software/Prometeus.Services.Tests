using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Prometeus.Services.Tests.Utils
{
    public static class ValidationHelper
    {
        public static (bool IsValid, List<ValidationResult> Results) Validate(object obj)
        {
            var context = new ValidationContext(obj);
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
            return (isValid, results);
        }
    }
}
