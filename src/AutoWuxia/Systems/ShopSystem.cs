using AutoWuxia.Characters;
using AutoWuxia.Config;
using AutoWuxia.Items;

namespace AutoWuxia.Systems;

/// <summary>
/// 商店商品条目
/// </summary>
public class ShopItem
{
    public string ItemId { get; set; } = "";
    public int Price { get; set; }
    public int Stock { get; set; } = -1;     // -1 表示无限库存
    public bool IsFixed { get; set; }        // 是否为固定商品（不随月度刷新）
}

/// <summary>
/// 商贩交易系统
/// </summary>
public static class ShopSystem
{
    /// <summary>
    /// 为商贩初始化当前商品列表（首次加载时调用）
    /// </summary>
    public static void InitShopItems(NPC merchant, ConfigManager configMgr)
    {
        if (merchant.CurrentShopItems.Count > 0) return;
        RefreshShopItems(merchant, configMgr, forceRefresh: true);
    }

    /// <summary>
    /// 刷新商贩的随机商品（月度调用）
    /// </summary>
    public static void RefreshShopItems(NPC merchant, ConfigManager configMgr, bool forceRefresh = false)
    {
        var fixedItems = merchant.CurrentShopItems.Where(i => i.IsFixed).ToList();
        var randomItems = forceRefresh
            ? new List<ShopItem>()
            : merchant.CurrentShopItems.Where(i => !i.IsFixed).ToList();

        if (forceRefresh && merchant.ShopRandomItems.Count > 0)
        {
            int count = Math.Min(merchant.ShopRandomItems.Count, Random.Shared.Next(3, 6));
            var pool = merchant.ShopRandomItems.ToList();
            Shuffle(pool);
            for (int i = 0; i < count; i++)
            {
                string itemId = pool[i];
                if (configMgr.Items.TryGetValue(itemId, out var itemCfg))
                {
                    randomItems.Add(new ShopItem
                    {
                        ItemId = itemId,
                        Price = itemCfg.Value,
                        Stock = Random.Shared.Next(1, 6),
                        IsFixed = false
                    });
                }
            }
        }

        if (fixedItems.Count == 0)
        {
            foreach (var itemId in merchant.ShopFixedItems)
            {
                if (configMgr.Items.TryGetValue(itemId, out var itemCfg))
                {
                    fixedItems.Add(new ShopItem
                    {
                        ItemId = itemId,
                        Price = itemCfg.Value,
                        Stock = -1,
                        IsFixed = true
                    });
                }
            }
        }

        merchant.CurrentShopItems = fixedItems.Concat(randomItems).ToList();
    }

    /// <summary>
    /// 购买商品
    /// </summary>
    public static (bool Success, string Message) BuyItem(NPC merchant, int shopIndex, Player player, ConfigManager configMgr)
    {
        if (shopIndex < 0 || shopIndex >= merchant.CurrentShopItems.Count)
            return (false, "商品不存在");

        var shopItem = merchant.CurrentShopItems[shopIndex];
        if (shopItem.Stock == 0)
            return (false, "该商品已售罄");

        if (!configMgr.Items.TryGetValue(shopItem.ItemId, out var itemCfg))
            return (false, "商品信息异常");

        if (player.Gold < shopItem.Price)
            return (false, $"银两不足（需要{shopItem.Price}两，当前{player.Gold}两）");

        player.Gold -= shopItem.Price;
        if (shopItem.Stock > 0) shopItem.Stock--;

        var item = ConfigManager.ItemFromConfig(itemCfg);
        item.Quantity = 1;
        player.Inventory.AddItem(item);

        return (true, $"购买 {itemCfg.Name} 成功，花费 {shopItem.Price} 两");
    }

    /// <summary>
    /// 获取NPC角色类型的中文名称
    /// </summary>
    public static string GetRoleDisplayName(string? role) => role switch
    {
        "wine_merchant" => "酒贩",
        "weapon_merchant" => "武器商",
        "medicine_merchant" => "药商",
        "blacksmith" => "铁匠",
        "martial_instructor" => "武术教头",
        "craft_teacher" => "技艺师傅",
        "quest_giver" => "门派执事",
        "errand_npc" => "委托人",
        "grocery_merchant" => "杂货商贩",
        "book_merchant" => "书贩",
        "hunter" => "猎人",
        _ => ""
    };

    /// <summary>
    /// 判断NPC是否为商贩
    /// </summary>
    public static bool IsMerchant(string? role) =>
        role is "wine_merchant" or "weapon_merchant" or "medicine_merchant" or "blacksmith"
            or "grocery_merchant" or "book_merchant" or "hunter";

    /// <summary>
    /// 出售物品给商贩（售价=物品价值的50%）
    /// </summary>
    public static (bool Success, string Message) SellItem(Player player, string itemId, int quantity, ConfigManager configMgr)
    {
        var item = player.Inventory.GetItem(itemId);
        if (item == null || item.Quantity < quantity)
            return (false, "背包中没有该物品");

        if (!configMgr.Items.TryGetValue(itemId, out var itemCfg))
            return (false, "物品信息异常");

        int sellPrice = Math.Max(1, itemCfg.Value / 2) * quantity;
        int removed = player.Inventory.RemoveItem(itemId, quantity);
        if (removed == 0)
            return (false, "出售失败");

        player.Gold += sellPrice;
        return (true, $"出售 {itemCfg.Name} x{removed}，获得 {sellPrice} 银");
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
