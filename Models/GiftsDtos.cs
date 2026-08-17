namespace AuthApi.Models;

public class GiftTierDto
{
    public int Points { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleUz { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DescriptionUz { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
}

public class GiftCampaignDto
{
    public string Name { get; set; } = string.Empty;
    public string NameUz { get; set; } = string.Empty;
    public DateTime AnnouncementDate { get; set; }
    public DateTime SelectionStartDate { get; set; }
    public DateTime SelectionEndDate { get; set; }
    public DateTime DistributionDate { get; set; }
    public bool IsActive { get; set; }
}

public class CreateGiftClaimDto
{
    public int CampaignId { get; set; }
    public int GiftTierId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateGiftClaimDto
{
    public string Status { get; set; } = string.Empty;
}
