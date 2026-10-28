import tuanjie from 'libtuanjie.so';

export class Tuanjie {
  /**
   * Notifies engine TuanjiePlayerAbility created.
   */
  public static nativeOnCreate(): void {
    tuanjie.nativeOnCreate();
  }

  /**
   * Notifies engine TuanjiePlayerAbility destroyed.
   */
  public static nativeOnDestroy(): void {
    tuanjie.nativeOnDestroy();
  }

  /**
   * Pause engine loop.
   */
  public static nativeOnPause(): void {
    tuanjie.nativeOnPause();
  }

  /**
   * Resume engine loop.
   */
  public static nativeOnResume(): void {
    tuanjie.nativeOnResume();
  }

  /**
   * Notifies engine windowstage actived.
   */
  public static nativeOnWindowStageActive(): void {
    tuanjie.nativeOnWindowStageActive();
  }

  /**
   * Notifies engine windowstage inacttived.
   */
  public static nativeOnWindowStageInActive(): void {
    tuanjie.nativeOnWindowStageInActive();
  }

  /**
   * Init main worker.
   */
  public static nativeSetWorker(): void {
    tuanjie.nativeSetWorker();
  }

  /**
   * Process message to ui thread.
   */
  public static nativeProcessUIThreadMessage(): void {
    tuanjie.nativeProcessUIThreadMessage();
  }

  /**
   * Calls the method named methodName on every MonoBehaviour in this game object.
   * @param methodName - Name of the method to call.
   * @param value - Optional parameter for the method.
   * @param options - Should an error be raised if the target object doesn't implement the method for the message?
   */
  public static TuanjieSendMessage(methodName: string, value: ESObject, options: ESObject): void {
    tuanjie.TuanjieSendMessage(methodName, value, options);
  }

  /**
   * Notifies engine to update the input string.
   */
  public static nativeSetInputString(): void {
    tuanjie.nativeSetInputString();
  }

  /**
   * Notifies engine softinput closed.
   */
  public static nativeSoftInputClosed(): void {
    tuanjie.nativeSoftInputClosed();
  }

  /**
   * Set input selection.
   * @param start - The starting position of the selected text.
   * @param length - The end position of the selected text.
   */
  public static nativeSetInputSelection(start: number, length: number): void {
    tuanjie.nativeSetInputSelection(start, length);
  }

  /**
   * Notifies engine display changed.
   */
  public static nativeOnDisplayChanged(): void {
    tuanjie.nativeOnDisplayChanged();
  }

  /**
   * Request the user to grant access to a device resource or information that requires authorization.
   * @param permissions - Permission array.
   * @param result - Return status code.
   * @param onGranted - Triggered when the user chooses Allow for permissions request.
   * @param onDenied - Triggered when the user chooses Deny for permissions request.
   */
  public static nativeGetPermissionRequestResult(permissions: string[], result: number, onGranted: ESObject, onDenied: ESObject): void {
    tuanjie.nativeGetPermissionRequestResult(permissions, result, onGranted, onDenied);
  }

  /**
   * Check if the user has granted access to a device resource or information that requires authorization.
   * @param permission - Permission name.
   * @param result - Return status code.
   * @param onAuthorized - Triggered when the permission is authorized.
   * @param onUnauthorized - Triggered when the permission is unauthorized.
   */
  public static nativeGetPermissionAuthorizeResult(permission: string, result: number, onAuthorized: ESObject, onUnauthorized: ESObject): void {
    tuanjie.nativeGetPermissionAuthorizeResult(permission, result, onAuthorized, onUnauthorized);
  }

  /**
   * Notifies engine location changed.
   * @param location - location data.
   */
  public static nativeOnLocationChange(location: ESObject): void {
    tuanjie.nativeOnLocationChange(location);
  }

  /**
   * native call js method.
   * @param resolved - Promise resolved.
   * @param userData - userData.
   * @param result - result.
   */
  public static nativeInvokeJSResult(resolved: boolean, userData: ESObject, result: ESObject): void {
    tuanjie.nativeInvokeJSResult(resolved, userData, result);
  }

  /**
   * Whether to render outside the safe area.
   */
  public static nativeGetIsRenderOutsizeSafeArea(): boolean {
    return tuanjie.nativeGetIsRenderOutsizeSafeArea();
  }

  /**
   * Get is avoid area changed.
   */
  public static nativeGetIsAvoidAreaChange(safeArea: ESObject): void {
    tuanjie.nativeGetIsAvoidAreaChange(safeArea);
  }

  /**
   * DebuggerDialog closed or not.
   */
  public static nativeDebuggerDialogClosed(): void {
    tuanjie.nativeDebuggerDialogClosed();
  }
}
