import React, { createContext, useState, useContext, useEffect, useCallback } from 'react';
import { auth } from './api';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const syncSession = useCallback(async () => {
    try {
      const response = await auth.getSession();
      const session = response.data;
      if (session?.isAuthenticated) {
        setUser({
          id: session.userId,
          username: session.username,
          email: session.email,
        });
      } else {
        setUser(null);
      }
      return true;
    } catch {
      setUser(null);
      return false;
    }
  }, []);

  // Initialize from server-side session cookie on mount
  useEffect(() => {
    const initializeAuth = async () => {
      await syncSession();
      setLoading(false);
    };

    initializeAuth();
  }, [syncSession]);

  const register = useCallback(async (username, email, password, firstName, lastName) => {
    setError(null);
    try {
      await auth.register(username, email, password, firstName, lastName);
      // After successful registration, you might want to auto-login or redirect to login
      return { success: true };
    } catch (err) {
      const message = err.response?.data?.message || 'Registration failed';
      setError(message);
      return { success: false, error: message };
    }
  }, []);

  const login = useCallback(async (username, password) => {
    setError(null);
    try {
      await auth.login(username, password);
      const synced = await syncSession();
      if (!synced) {
        throw new Error('Unable to establish authenticated session.');
      }

      return { success: true, user };
    } catch (err) {
      const message = err.response?.data?.message || 'Login failed';
      setError(message);
      return { success: false, error: message };
    }
  }, [syncSession, user]);

  const logout = useCallback(() => {
    auth.logout().catch(() => {
      // Ignore logout API errors and clear client state anyway.
    });

    setUser(null);
    setError(null);
  }, []);

  const refreshAccessToken = useCallback(async () => {
    try {
      await auth.refreshToken();
      return await syncSession();
    } catch (err) {
      console.error('Token refresh failed:', err);
      logout();
      return false;
    }
  }, [logout, syncSession]);

  const isAuthenticated = !!user;

  const value = {
    user,
    loading,
    error,
    isAuthenticated,
    login,
    register,
    logout,
    refreshAccessToken,
    setError,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
