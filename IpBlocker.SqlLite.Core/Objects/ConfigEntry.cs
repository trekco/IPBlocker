﻿using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace IpBlocker.SqlLite.Core.Objects
{

  
    public class ConfigEntry
    {

        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; }
        public string Value { get; set; }
    }
}
