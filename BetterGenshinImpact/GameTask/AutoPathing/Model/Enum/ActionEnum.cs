﻿using System.Collections.Generic;

namespace BetterGenshinImpact.GameTask.AutoPathing.Model.Enum;

public class ActionEnum(string code, string msg)
{
    public static readonly ActionEnum StopFlying = new("stop_flying", "下落攻击");
    // 还有要加入的其他动作
    // 滚轮F
    // 触发自动战斗任务
    // 执行 js 脚本(推荐)
    // 执行键鼠脚本
    // 纳西达采集

    public static IEnumerable<ActionEnum> Values
    {
        get
        {
            yield return StopFlying;
        }
    }

    public string Code { get; private set; } = code;
    public string Msg { get; private set; } = msg;

    public static string GetMsgByCode(string code)
    {
        foreach (var item in Values)
        {
            if (item.Code == code)
            {
                return item.Msg;
            }
        }
        return code;
    }
}
