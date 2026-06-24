import requests

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
TIMEOUT = 30

def test_update_lead_status_with_valid_data():
    session = requests.Session()
    
    # 1. Login
    login_payload = {'email': 'admin@carstore.com', 'password': 'Admin123!'}
    login_resp = session.post(LOGIN_URL, json=login_payload, timeout=TIMEOUT)
    assert login_resp.status_code == 200, f"Login failed: {login_resp.text}"
    token = login_resp.json().get("token")
    assert token, "Token not found"
    
    headers = {
        'Authorization': f'Bearer {token}',
        'Content-Type': 'application/json'
    }
    
    # 2. Create lead
    lead_payload = {
        'clientName': 'John Doe',
        'email': 'johndoe@example.com',
        'phone': '1234567890',
        'source': 0, # Web
        'notes': 'Test notes',
        'interestedVehicleId': None
    }
    
    create_resp = session.post(LEADS_URL, headers=headers, json=lead_payload, timeout=TIMEOUT)
    assert create_resp.status_code == 201, f"Create lead failed: {create_resp.text}"
    lead_data = create_resp.json()
    lead_id = lead_data.get("id")
    assert lead_id, "Lead ID not found in response"
    
    # 3. Update status (PATCH)
    status_payload = {
        'newStatus': 1, # Contactado
        'notes': 'Called lead'
    }
    status_resp = session.patch(f"{LEADS_URL}/{lead_id}/status", headers=headers, json=status_payload, timeout=TIMEOUT)
    assert status_resp.status_code == 204, f"Update status failed: {status_resp.status_code}, {status_resp.text}"
    
    # 4. Fetch and verify
    get_resp = session.get(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
    assert get_resp.status_code == 200, f"Get lead failed: {get_resp.text}"
    get_data = get_resp.json()
    # Check both int and string just in case EF/API converts representation
    assert get_data.get("status") in (1, "Contactado"), f"Status mismatch: {get_data.get('status')}"
    
    # Cleanup
    try:
        session.delete(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
    except Exception as e:
        print(f"Cleanup warning: {e}")

if __name__ == '__main__':
    test_update_lead_status_with_valid_data()
    print("TC002 PASSED")
