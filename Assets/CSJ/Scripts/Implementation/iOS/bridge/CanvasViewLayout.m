//
//  BUMCanvasView+Layout.m
//  UnityFramework
//
//  Created by ByteDance on 2022/7/13.
//

#import "CanvasViewLayout.h"

static CGFloat const margin = 15;
static CGSize const logoSize = { 15, 15 };
static UIEdgeInsets const padding = { 10, 15, 10, 15 };

@implementation CanvasViewLayout

+ (void)exampleLayoutWithFrame:(CGRect)frame canvasView:(BUMCanvasView *)canvasView {
    //    NSInteger index = [canvasView.canvasViews indexOfObject:canvasView];
    UIImageView *imageView1 = [[UIImageView alloc] init];
    UIImageView *imageView2 = [[UIImageView alloc] init];
    canvasView.frame = frame;
    
    CGFloat width = CGRectGetWidth(canvasView.bounds);
    CGFloat contentWidth = (width - 2 * margin);
    CGFloat y = padding.top;
    
    // 确定是自渲染才会调用该方法
    //    canvasView.descLabel = [[UILabel alloc] initWithFrame:CGRectMake(0, y, contentWidth, 40)];
    canvasView.descLabel.text = canvasView.data.AdTitle;
    canvasView.descLabel.backgroundColor = [UIColor grayColor];
    y += 40;
    y += 5;
    
    CGFloat leftMargin = frame.size.width/20;
    if (canvasView.data.icon.imageURL) {
        CGFloat cusIconWidth = 30;
        CGFloat cusIconHeight = 30;
        
        canvasView.iconImageView = [[UIImageView alloc] initWithFrame:CGRectMake(0, y, cusIconWidth, cusIconHeight)];
        UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:canvasView.data.icon.imageURL]]];
        [canvasView.iconImageView setImage:imagePic];
        
        canvasView.descLabel.frame = CGRectMake(CGRectGetMaxX(canvasView.iconImageView.frame), CGRectGetMinY(canvasView.iconImageView.frame), frame.size.width - leftMargin - CGRectGetMaxX(canvasView.iconImageView.frame), CGRectGetHeight(canvasView.iconImageView.frame));
    }
    
    CGFloat dislikeX = width - 24;
    // 物料信息可能不包含关闭按钮需要自己实现
    if (!canvasView.dislikeBtn) {
        canvasView.dislikeBtn = [[UIButton alloc] init];
        [canvasView.dislikeBtn setImage:[UIImage imageNamed:@"feedClose"] forState:UIControlStateNormal];
        canvasView.dislikeBtn.backgroundColor = [UIColor cyanColor];
        canvasView.dislikeBtn.userInteractionEnabled = YES;
    }
    canvasView.dislikeBtn.frame = CGRectMake(dislikeX-24, 0, 24, 24);
    
    CGFloat originInfoX = padding.left;
    if (canvasView.adLogoView) {
        canvasView.adLogoView.frame = CGRectMake(originInfoX, y + 3, 26, 14);
        originInfoX += 24;
        originInfoX += 10;
    }
    
    canvasView.titleLabel.text = canvasView.data.AdDescription;
    canvasView.titleLabel.frame = CGRectMake(0, CGRectGetMaxY(canvasView.descLabel.frame), contentWidth, 40);
    canvasView.titleLabel.backgroundColor = [UIColor grayColor];
    BUMaterialMeta *adMeta = canvasView.data;
    
    if (canvasView.hasSupportActionBtn) {
        CGFloat customBtnWidth = 100;
        canvasView.callToActionBtn.frame = CGRectMake(dislikeX - customBtnWidth - 5, CGRectGetMaxY(canvasView.titleLabel.frame), customBtnWidth, 20);
        NSString *btnTxt = @"Click";
        if (canvasView.data.buttonText.length > 0) {
            btnTxt = canvasView.data.buttonText;
        }
        [canvasView.callToActionBtn setTitle:btnTxt forState:UIControlStateNormal];
        canvasView.callToActionBtn.backgroundColor = [UIColor redColor];
    }
    
    // imageMode decides whether to show video or not
    if (adMeta.imageMode == BUFeedVideoAdModeImage) {
        canvasView.imageView.hidden = YES;
        if (canvasView.mediaView) {
            BUImage *image = canvasView.data.imageAry.firstObject;
            const CGFloat imageHeight = contentWidth * (image.height / image.width);
            canvasView.mediaView.frame = CGRectMake(0, CGRectGetMaxY(canvasView.titleLabel.frame), contentWidth, imageHeight);
        }
    } else if (adMeta.imageMode == BUFeedADModeLargeImage) {
        canvasView.imageView.hidden = NO;
        if (adMeta.imageAry.count > 0) {
            BUImage *image = canvasView.data.imageAry.firstObject;
            const CGFloat imageHeight = contentWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                
                canvasView.imageView.frame = CGRectMake(5, CGRectGetMaxY(canvasView.titleLabel.frame), contentWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:canvasView.data.icon.imageURL]]];
                [canvasView.imageView setImage:imagePic];
            }
        }
    } else if (adMeta.imageMode == BUFeedADModeGroupImage) {
        canvasView.imageView.hidden = NO;
        CGFloat y = CGRectGetMaxY(canvasView.titleLabel.frame);
        if (canvasView.callToActionBtn.frame.origin.y != 0) {
            y = CGRectGetMaxY(canvasView.callToActionBtn.frame);
        }
        if (adMeta.imageAry.count > 1) {
            CGFloat imageWidth = (contentWidth - 5 * 2) / 3;
            BUImage *image = adMeta.imageAry[1];
            const CGFloat imageHeight = imageWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                canvasView.imageView.frame = CGRectMake(5, y + 5, imageWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [canvasView.imageView setImage:imagePic];
            }
        }
        if (adMeta.imageAry.count > 2) {
            CGFloat imageWidth = (contentWidth - 5 * 2) / 3;
            BUImage *image = adMeta.imageAry[2];
            const CGFloat imageHeight = imageWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                
                imageView1.frame = CGRectMake(5+imageWidth+10, y + 5, imageWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [imageView1 setImage:imagePic];
                [canvasView addSubview:imageView1];
            }
        }
        if (adMeta.imageAry.count > 3) {
            CGFloat imageWidth = (contentWidth - 5 * 2) / 3;
            BUImage *image = adMeta.imageAry[3];
            const CGFloat imageHeight = imageWidth * (image.height / image.width);
            if (image.imageURL.length > 0) {
                imageView2.frame = CGRectMake(5+imageWidth*2+10+10, y + 5, imageWidth, imageHeight);
                UIImage *imagePic = [UIImage imageWithData:[NSData dataWithContentsOfURL:[NSURL URLWithString:image.imageURL]]];
                [imageView2 setImage:imagePic];
                [canvasView addSubview:imageView2];
            }
        }
    }
    
    [canvasView bringSubviewToFront:canvasView.dislikeBtn];
    canvasView.frame = CGRectMake(frame.origin.x, frame.origin.y, frame.size.width, CGRectGetHeight(canvasView.titleLabel.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.mediaView.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.imageView.frame)+CGRectGetHeight(canvasView.descLabel.frame)+CGRectGetHeight(canvasView.callToActionBtn.frame)+CGRectGetHeight(canvasView.callToActionBtn.frame)+CGRectGetHeight(canvasView.dislikeBtn.frame));
    
    // Register UIView with the native ad; the whole UIView will be clickable.
    [canvasView registerClickableViews:@[canvasView.callToActionBtn,
                                   canvasView.titleLabel,
                                   canvasView.descLabel,
                                   canvasView.imageView,
                                   imageView1,
                                   imageView2]];
    
}

@end
