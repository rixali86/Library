namespace Library.Models
{
    public class Title 
    {
        public int TitleId { get; set; }
        public string TitleName { get; set; } = string.Empty;
        public string? Isbn { get; set; }
        public int? PublicationYear { get; set; }
        public string? Publisher { get; set; }


        public string Author { get; set; } = string.Empty; // <-- added
    }
    public class Author
    {
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
    }

    public class BookCopy
    {
        public int CopyId { get; set; }
        public int TitleId { get; set; }
        public int BranchId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string ConditionStatus { get; set; } = "Good";
        public Title? Title { get; set; }
        public Branch? Branch { get; set; }
    }


}



