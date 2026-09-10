/*
  COPYRIGHT 2024 - PROPERTY OF TOBII AB
  -------------------------------------
  2015 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

package com.tobii;

import android.content.Context;
import android.content.res.AssetManager;
import android.util.Log;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;

public final class TobiiUnityBridge {
    private static final String TAG = "TobiiUnityBridge";
    
    static {
        try {
            System.loadLibrary("tobii_stream_engine");
        } catch (UnsatisfiedLinkError e) {
            Log.w(TAG, "tobii_stream_engine library not found: " + e.getMessage());
        }
    }

    // Stub interface to replace TobiiBinderCallbackHandler
    private interface BinderCallbackHandler {
        enum Error {
            NO_ERROR,
            BINDER_NOT_AVAILABLE
        }
        void ready(Error e);
    }

    // Stub class to replace TobiiBinder
    private static class BinderStub {
        protected Object realBinder = null;
        
        public boolean start(Context ctx, BinderCallbackHandler handler) {
            if (realBinder != null) {
                try {
                    // Create a proxy for the real callback handler
                    Class<?> callbackClass = Class.forName("com.tobii.binder.TobiiBinderCallbackHandler");
                    Object callbackProxy = Proxy.newProxyInstance(
                        callbackClass.getClassLoader(),
                        new Class<?>[] { callbackClass },
                        new InvocationHandler() {
                            @Override
                            public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
                                if ("ready".equals(method.getName()) && args != null && args.length > 0) {
                                    // Convert real Error enum to our stub Error enum
                                    Object realError = args[0];
                                    BinderCallbackHandler.Error stubError;
                                    if (realError == null) {
                                        stubError = null;
                                    } else {
                                        String errorName = realError.toString();
                                        if ("NO_ERROR".equals(errorName)) {
                                            stubError = BinderCallbackHandler.Error.NO_ERROR;
                                        } else {
                                            stubError = BinderCallbackHandler.Error.BINDER_NOT_AVAILABLE;
                                        }
                                    }
                                    handler.ready(stubError);
                                }
                                return null;
                            }
                        }
                    );
                    
                    // Call the real binder's start method
                    Method startMethod = realBinder.getClass().getMethod("start", Context.class, callbackClass);
                    Object result = startMethod.invoke(realBinder, ctx, callbackProxy);
                    return result instanceof Boolean ? (Boolean) result : false;
                } catch (Exception e) {
                    Log.e(TAG, "Failed to start real binder: " + e.getMessage(), e);
                }
            }
            return false;
        }
        
        public boolean isReady() {
            if (realBinder != null) {
                try {
                    Method isReadyMethod = realBinder.getClass().getMethod("isReady");
                    Object result = isReadyMethod.invoke(realBinder);
                    return result instanceof Boolean ? (Boolean) result : false;
                } catch (Exception e) {
                    Log.e(TAG, "Failed to check real binder isReady: " + e.getMessage());
                }
            }
            return false;
        }
    }

    private final BinderStub binder;
    private final AssetManager assets;
    private volatile boolean ready = false;
    private volatile BinderCallbackHandler.Error lastError = null;

    public TobiiUnityBridge(Context ctx) {
        logDebug("Constructor invoked");
        Context app = ctx.getApplicationContext();
        assets = app.getAssets();
        logDebug("AssetManager acquired");
        
        // Try to create real binder via reflection, fall back to stub
        binder = createBinder();
        logDebug("Binder created: " + (binder != null) + ", has real binder: " + (binder != null && binder.realBinder != null));
        
        if (binder != null) {
            try {
                boolean ok = binder.start(app, new BinderCallbackHandler() {
                    @Override
                    public void ready(Error e) {
                        TobiiUnityBridge.this.onReady(e);
                    }
                });
                logDebug("start() returned=" + ok);
            } catch (Throwable t) {
                logError("binder.start() threw: " + t.getClass().getName() + " " + t.getMessage());
                onReady(BinderCallbackHandler.Error.BINDER_NOT_AVAILABLE);
            }
        } else {
            logError("binder is null, start() skipped");
            onReady(BinderCallbackHandler.Error.BINDER_NOT_AVAILABLE);
        }
        logDebug("Constructor done, awaiting ready() callback");
    }

    private BinderStub createBinder() {
        BinderStub stub = new BinderStub();
        try {
            // Try to load the real TobiiBinderFactory via reflection
            Class<?> factoryClass = Class.forName("com.tobii.binder.TobiiBinderFactory");
            Method createMethod = factoryClass.getMethod("create");
            Object realBinder = createMethod.invoke(null);
            
            if (realBinder != null) {
                logDebug("Real TobiiBinder loaded via reflection");
                stub.realBinder = realBinder;
            } else {
                logWarning("TobiiBinderFactory.create() returned null");
            }
        } catch (ClassNotFoundException e) {
            logWarning("TobiiBinderFactory not found - using stub implementation");
        } catch (Exception e) {
            logError("Failed to create real binder: " + e.getMessage());
        }
        
        return stub;
    }

    // Helper method for debug logging that forwards to Unity
    private void logDebug(String message) {
        Log.d(TAG, message);
        sendUnityLog(TAG + ": " + message);
    }

    // Helper method for error logging that forwards to Unity
    private void logError(String message) {
        Log.e(TAG, message);
        sendUnityLog(TAG + " [ERROR]: " + message);
    }

    // Helper method for warning logging that forwards to Unity
    private void logWarning(String message) {
        Log.w(TAG, message);
        sendUnityLog(TAG + " [WARN]: " + message);
    }

    private void sendUnityLog(String text) {
        try {
            // Replace "GlobalLogger" with your actual GameObject name
            com.unity3d.player.UnityPlayer.UnitySendMessage("GlobalLogger", "OnJavaLog", text);
        } catch (Throwable t) {
            // Don't log this error to avoid infinite recursion
            Log.w(TAG, "UnitySendMessage failed: " + t.getMessage());
        }
    }

    private void onReady(BinderCallbackHandler.Error e) {
        lastError = e;
        ready = (e == BinderCallbackHandler.Error.NO_ERROR);
        String msg = "ready() callback, error=" + (e == null ? "<null>" : e.name()) + ", ready=" + ready;
        logWarning(msg);
    }

    public boolean isReady() { return ready || (binder != null && binder.isReady()); }
    public String lastErrorName() { return lastError == null ? "<null>" : lastError.name(); }
    public AssetManager getAssetManager() { return assets; }
}