// Error notification system for 4U.chat
window.errorNotifications = {
    // Show error toast notification
    showError: function(message, type = 'error') {
        const toast = document.createElement('div');
        toast.className = `error-toast error-toast-${type}`;
        
        const icon = type === 'error' ? '🚨' : type === 'warning' ? '⚠️' : 'ℹ️';
        
        toast.innerHTML = `
            <div class="error-toast-content">
                <span class="error-toast-icon">${icon}</span>
                <span class="error-toast-message">${message}</span>
                <button class="error-toast-close" onclick="this.parentElement.parentElement.remove()">×</button>
            </div>
        `;
        
        // Add CSS styles if not already present
        this.addStyles();
        
        // Add to page
        document.body.appendChild(toast);
        
        // Auto-remove after 8 seconds for errors, 5 seconds for others
        const timeout = type === 'error' ? 8000 : 5000;
        setTimeout(() => {
            if (toast.parentElement) {
                toast.style.opacity = '0';
                setTimeout(() => toast.remove(), 300);
            }
        }, timeout);
        
        // Animate in
        setTimeout(() => toast.classList.add('error-toast-show'), 10);
    },
    
    // Add CSS styles for error toasts
    addStyles: function() {
        if (document.querySelector('#error-toast-styles')) return;
        
        const styles = document.createElement('style');
        styles.id = 'error-toast-styles';
        styles.textContent = `
            .error-toast {
                position: fixed;
                top: 20px;
                right: 20px;
                max-width: 400px;
                z-index: 10000;
                transform: translateX(100%);
                transition: all 0.3s ease;
                opacity: 0;
            }
            
            .error-toast-show {
                transform: translateX(0);
                opacity: 1 !important;
            }
            
            .error-toast-content {
                background: var(--error-bg, #fee);
                border: 1px solid var(--error-border, #fcc);
                border-radius: 8px;
                padding: 12px 16px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                display: flex;
                align-items: flex-start;
                gap: 10px;
            }
            
            .error-toast-error .error-toast-content {
                background: #fee;
                border-color: #fcc;
                color: #c53030;
            }
            
            .error-toast-warning .error-toast-content {
                background: #fef5e7;
                border-color: #f6e05e;
                color: #d69e2e;
            }
            
            .error-toast-info .error-toast-content {
                background: #ebf8ff;
                border-color: #90cdf4;
                color: #3182ce;
            }
            
            .error-toast-icon {
                font-size: 18px;
                flex-shrink: 0;
            }
            
            .error-toast-message {
                flex: 1;
                font-size: 14px;
                line-height: 1.4;
            }
            
            .error-toast-close {
                background: none;
                border: none;
                font-size: 18px;
                cursor: pointer;
                padding: 0;
                width: 20px;
                height: 20px;
                display: flex;
                align-items: center;
                justify-content: center;
                opacity: 0.6;
                flex-shrink: 0;
            }
            
            .error-toast-close:hover {
                opacity: 1;
            }
            
            /* Dark theme support */
            body.dark-theme .error-toast-error .error-toast-content,
            body:not(.light-theme) .error-toast-error .error-toast-content {
                background: #2d1b1b;
                border-color: #4a2626;
                color: #fc8181;
            }
            
            body.dark-theme .error-toast-warning .error-toast-content,
            body:not(.light-theme) .error-toast-warning .error-toast-content {
                background: #2d2416;
                border-color: #4a3c1d;
                color: #f6e05e;
            }
            
            body.dark-theme .error-toast-info .error-toast-content,
            body:not(.light-theme) .error-toast-info .error-toast-content {
                background: #1a2332;
                border-color: #2c5282;
                color: #90cdf4;
            }
        `;
        
        document.head.appendChild(styles);
    },
    
    // Show streaming error notification
    showStreamingError: function(errorMessage, isRetryable = false) {
        const type = isRetryable ? 'warning' : 'error';
        const prefix = isRetryable ? 'Streaming issue (retrying)' : 'Streaming failed';
        this.showError(`${prefix}: ${errorMessage}`, type);
    },
    
    // Show API key error
    showApiKeyError: function() {
        this.showError('OpenRouter API key is missing or invalid. Please check your settings.', 'error');
    },
    
    // Show rate limit warning
    showRateLimit: function() {
        this.showError('Rate limit reached. Please wait a moment before sending another message.', 'warning');
    },
    
    // Show network error
    showNetworkError: function() {
        this.showError('Network connection issue. Please check your internet connection.', 'error');
    }
};

// Make it available globally
window.showErrorNotification = window.errorNotifications.showError.bind(window.errorNotifications);
window.showStreamingError = window.errorNotifications.showStreamingError.bind(window.errorNotifications);
window.showApiKeyError = window.errorNotifications.showApiKeyError.bind(window.errorNotifications);
window.showRateLimit = window.errorNotifications.showRateLimit.bind(window.errorNotifications);
window.showNetworkError = window.errorNotifications.showNetworkError.bind(window.errorNotifications);