using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp1.Models
{
    public class Request
    {
        public string model { get; set; }
        public List<Message> messages { get; set; }
        public bool stream { get; set; }
        public int repetition_penalty { get; set; }
        public class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }
    }
}
