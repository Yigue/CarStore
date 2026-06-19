import requests

BASE_URL = "http://localhost:8080"
LOGIN_URL = f"{BASE_URL}/api/v1/users/login"
LEADS_URL = f"{BASE_URL}/api/v1/leads"
CARS_URL = f"{BASE_URL}/api/v1/cars"
TIMEOUT = 30

def test_create_lead_with_vehicle_and_contact_info():
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
    
    # 2. Get valid car ID
    cars_resp = session.get(CARS_URL, headers=headers, timeout=TIMEOUT)
    assert cars_resp.status_code == 200, f"Get cars failed: {cars_resp.text}"
    cars_data = cars_resp.json()
    items = cars_data.get("items", [])
    assert len(items) > 0, "No cars found in database"
    car_id = items[0]["id"]
    
    # 3. Create lead
    lead_payload = {
        'clientName': 'John Doe',
        'email': 'johndoe@example.com',
        'phone': '1234567890',
        'source': 0, # Web
        'notes': 'Test notes',
        'interestedVehicleId': car_id
    }
    
    create_resp = session.post(LEADS_URL, headers=headers, json=lead_payload, timeout=TIMEOUT)
    assert create_resp.status_code == 201, f"Create lead failed: {create_resp.text}"
    lead_data = create_resp.json()
    lead_id = lead_data.get("id")
    assert lead_id, "Lead ID not found in response"
    
    # Cleanup
    try:
        del_resp = session.delete(f"{LEADS_URL}/{lead_id}", headers=headers, timeout=TIMEOUT)
        assert del_resp.status_code in (200, 204), f"Delete lead failed: {del_resp.text}"
    except Exception as e:
        print(f"Cleanup warning: {e}")

if __name__ == '__main__':
    test_create_lead_with_vehicle_and_contact_info()
    print("TC001 PASSED")