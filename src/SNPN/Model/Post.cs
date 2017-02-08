using System;

namespace SNPN.Model
{
	public class Post
	{
		public int Id { get; set; }
		public int ThreadId { get; set; }
		public int ParentId { get; set; }
		public string Author { get; set; }
		public string Category { get; set; }
		public DateTime Date { get; set; }
		public string Body { get; set; }
	}
}