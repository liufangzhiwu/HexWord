package com.bytedance.android;

import android.util.Log;

import com.bytedance.sdk.openadsdk.TTClientBidding;

/**
 * created by jijiachun on 2021/10/27
 */
public class ClientBiddingBridge {
    /* public static void win(Object adObject, double auctionBidToWin) {
        TTClientBidding ttClientBidding = checkObject(adObject);
        if (ttClientBidding != null) {
            ttClientBidding.win(auctionBidToWin);
            Log.e("ClientBiddingBridge", "win win win:" + auctionBidToWin);
        }
    }

    public static void lossNoPrice(Object adObject, String lossReason, String winBidder) {
        TTClientBidding ttClientBidding = checkObject(adObject);
        if (ttClientBidding != null) {
            ttClientBidding.loss(null, lossReason, winBidder);
            Log.e("ClientBiddingBridge", "lossNorPrice lossNorPrice lossNorPrice:");
        }
    }

    public static void loss(Object adObject, double auctionPrice, String lossReason, String winBidder) {
        TTClientBidding ttClientBidding = checkObject(adObject);
        if (ttClientBidding != null) {
            ttClientBidding.loss(auctionPrice, lossReason, winBidder);
            Log.e("ClientBiddingBridge", "loss2 loss2 loss2:" + auctionPrice);
        }
    }


    public static void setPriceNoPrice(Object adObject) {
        TTClientBidding ttClientBidding = checkObject(adObject);
        if (ttClientBidding != null) {
            ttClientBidding.setPrice(null);
            Log.e("ClientBiddingBridge", "setPriceNoArg setPriceNoArg setPriceNoArg:");
        }
    }

    public static void setPrice(Object adObject, double auctionPrice) {
        TTClientBidding ttClientBidding = checkObject(adObject);
        if (ttClientBidding != null) {
            ttClientBidding.setPrice(auctionPrice);
            Log.e("ClientBiddingBridge", "setPrice2 setPrice2 setPrice2:" + auctionPrice);
        }
    }

    private static TTClientBidding checkObject(Object adObject) {
        if (adObject instanceof TTClientBidding) {
            return (TTClientBidding) adObject;
        }
        return null;
    } */


    //将基础类型转换为Double类型
    public static Double toDouble(double value) {
        return new Double(value);
    }
}
